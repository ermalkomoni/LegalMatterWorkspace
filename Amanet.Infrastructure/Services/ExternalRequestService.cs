using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Amanet.Application.DTOs.Documents;
using Amanet.Application.Exceptions;
using Amanet.Application.Services;
using Amanet.Core.Entities;
using Amanet.Core.Enums;
using Amanet.Infrastructure.Data;
using Amanet.Infrastructure.Email;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Amanet.Infrastructure.Services;

public sealed class ExternalRequestService(ApplicationDbContext db, IEmailSender email,
    IOptions<ExternalLinkOptions> options, IDocumentService documents,
    IMatterActivityService activities, IAdminNotificationService notifications) : IExternalRequestService
{
    public async Task<IReadOnlyList<ExternalRequestDto>> ListAsync(int? matterId, DocumentRequestStatus? status,
        CancellationToken cancellationToken)
    {
        if (matterId.HasValue && !await db.Matters.AnyAsync(matter => matter.Id == matterId, cancellationToken))
            throw new ResourceNotFoundException("Matter not found.");
        var requests = await db.ExternalRequests.AsNoTracking().Include(request => request.DeliveryAttempts)
            .Include(request => request.Submissions).ThenInclude(submission => submission.DocumentVersion).AsSplitQuery()
            .Where(request => (!matterId.HasValue || request.MatterId == matterId) && (!status.HasValue || request.UploadStatus == status))
            .OrderByDescending(request => request.CreatedAtUtc)
            .ThenBy(request => request.Id).ToListAsync(cancellationToken);
        return requests.Select(ToDto).ToArray();
    }

    public async Task<ExternalRequestDto> GetAsync(int matterId, Guid requestId, CancellationToken cancellationToken)
    {
        var request = await db.ExternalRequests.AsNoTracking().Include(item => item.DeliveryAttempts)
            .Include(item => item.Submissions).ThenInclude(submission => submission.DocumentVersion).AsSplitQuery()
            .SingleOrDefaultAsync(item => item.MatterId == matterId && item.Id == requestId, cancellationToken)
            ?? throw new ResourceNotFoundException("Request not found.");
        return ToDto(request);
    }

    public async Task<ExternalRequestDto> CreateAsync(int matterId, CreateExternalRequest input,
        CancellationToken cancellationToken)
    {
        EnsureSendCanCommit();
        if (!Enum.IsDefined(input.Action) || string.IsNullOrWhiteSpace(input.Title) || input.Title.Length > 200 ||
            string.IsNullOrWhiteSpace(input.RecipientEmail) || input.RecipientEmail.Length > 254 ||
            !new EmailAddressAttribute().IsValid(input.RecipientEmail) ||
            input.RecipientEmail.Any(char.IsControl) ||
            (input.Action == ExternalAction.Upload && input.DocumentVersionId.HasValue) ||
            (input.Action == ExternalAction.Sign && !input.DocumentVersionId.HasValue))
            throw new FluentValidation.ValidationException(
                [new ValidationFailure("Request", "Invalid external request fields or document scope.")]);
        if (input.RecipientName.Length > 200 || input.Instructions.Length > 5000 ||
            (input.Action == ExternalAction.Upload && string.IsNullOrWhiteSpace(input.RecipientName)))
            throw new FluentValidation.ValidationException(
                [new ValidationFailure("RecipientName", "Provide a recipient name for the document request.")]);
        if (input.ExpiresAtUtc.HasValue && (input.ExpiresAtUtc <= DateTimeOffset.UtcNow ||
                input.ExpiresAtUtc > DateTimeOffset.UtcNow.AddDays(365)))
            throw new FluentValidation.ValidationException(
                [new ValidationFailure("ExpiresAtUtc", "Expiry must be in the future, within one year.")]);

        var matter = await db.Matters.SingleOrDefaultAsync(item => item.Id == matterId, cancellationToken)
            ?? throw new ResourceNotFoundException("Matter not found.");
        MatterRules.EnsureWritable(matter);
        if (input.DocumentVersionId.HasValue)
            await documents.EnsureSignableAsync(matterId, input.DocumentVersionId.Value, cancellationToken);

        var request = new ExternalRequest
        {
            Id = Guid.NewGuid(), Matter = matter, Action = input.Action,
            RecipientEmail = input.RecipientEmail.Trim(), Title = input.Title.Trim(),
            RecipientName = input.RecipientName.Trim(), Instructions = input.Instructions.Trim(),
            UploadStatus = input.Action == ExternalAction.Upload ? DocumentRequestStatus.AwaitingUpload : null,
            SignerKind = input.Action == ExternalAction.Sign ? SignerKind.ExternalClient : null,
            SigningConsentText = input.Action == ExternalAction.Sign ? SigningConsent.Text : null,
            Progress = input.Action == ExternalAction.Sign ? RequestProgress.Sent : RequestProgress.Pending,
            DocumentVersionId = input.DocumentVersionId, CreatedAtUtc = DateTimeOffset.UtcNow
        };
        db.ExternalRequests.Add(request);
        return await RotateAndSendAsync(request, InvitationFor(request.Action), cancellationToken, input.ExpiresAtUtc);
    }

    public async Task<ExternalRequestDto> ResendAsync(int matterId, Guid requestId, EmailTemplate? template,
        CancellationToken cancellationToken)
    {
        EnsureSendCanCommit();
        var request = await FindAsync(matterId, requestId, cancellationToken);
        MatterRules.EnsureWritable(request.Matter);
        if (request.Action == ExternalAction.Sign && request.SignerKind != SignerKind.ExternalClient)
            throw new ConflictException("Admin signing workflows do not send external links.");
        if (request.Action == ExternalAction.Upload && !CanUpload(request))
            throw new ConflictException("Only requests awaiting an upload or correction can be resent.");
        var selectedTemplate = template ?? InvitationFor(request.Action);
        if (!Enum.IsDefined(selectedTemplate) ||
            (selectedTemplate == EmailTemplate.DocumentRequest && request.Action != ExternalAction.Upload) ||
            (selectedTemplate == EmailTemplate.SignatureInvitation && request.Action != ExternalAction.Sign) ||
            (selectedTemplate == EmailTemplate.Correction && (request.Action != ExternalAction.Upload ||
                request.UploadStatus != DocumentRequestStatus.CorrectionsRequested)))
            throw new FluentValidation.ValidationException(
                [new ValidationFailure("Template", "The email template does not match the request action.")]);
        return await RotateAndSendAsync(request, selectedTemplate, cancellationToken);
    }

    public async Task<ExternalRequestDto> RevokeAsync(int matterId, Guid requestId, CancellationToken cancellationToken)
    {
        var request = await FindAsync(matterId, requestId, cancellationToken);
        // Refresh in case this context tracked the request before its latest rotation or revocation.
        await db.Entry(request).ReloadAsync(cancellationToken);
        await db.Entry(request.Matter).ReloadAsync(cancellationToken);
        if (request.RevokedAtUtc is null)
        {
            request.RevokedAtUtc = DateTimeOffset.UtcNow;
            activities.Record(request.Matter, MatterActivityType.ExternalRequestRevoked,
                $"External request #{request.Id} revoked.");
            await SaveAsync(request, cancellationToken);
        }
        return await GetAsync(matterId, requestId, cancellationToken);
    }

    public async Task<ExternalRequest> ValidateAsync(Guid requestId, ExternalAction action, string? token,
        bool forSubmission, CancellationToken cancellationToken)
    {
        // Tokens are 32 random bytes encoded as lowercase hex. Reject malformed headers before hashing.
        if (!Enum.IsDefined(action) || token is null || token.Length != 64 ||
            token.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            throw Unavailable();
        var request = await db.ExternalRequests.AsNoTracking().Include(item => item.Matter)
            .SingleOrDefaultAsync(item => item.Id == requestId && item.Action == action, cancellationToken);
        if (request is null || request.Matter.ArchivedAtUtc.HasValue || request.RevokedAtUtc.HasValue ||
            (action == ExternalAction.Sign && (request.SignerKind != SignerKind.ExternalClient || request.ReplacedAtUtc.HasValue ||
                (request.Progress != RequestProgress.Signed && string.IsNullOrWhiteSpace(request.SigningConsentText)))) ||
            request.ExpiresAtUtc <= DateTimeOffset.UtcNow ||
            !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(request.TokenHash), Hash(token)))
            throw Unavailable();
        if (forSubmission && (request.Action == ExternalAction.Upload ? !CanUpload(request) :
                request.Progress == RequestProgress.Signed))
            throw new ConflictException("This request is not accepting submissions.");
        if (request.Action == ExternalAction.Sign)
        {
            var documentId = await db.DocumentVersions.Where(version => version.Id == request.DocumentVersionId &&
                    version.Document.MatterId == request.MatterId).Select(version => (int?)version.DocumentId)
                .SingleOrDefaultAsync(cancellationToken);
            if (!documentId.HasValue || request.SigningDocumentId != documentId)
                throw Unavailable();
            await documents.EnsureSignableAsync(request.MatterId, request.DocumentVersionId!.Value, cancellationToken);
        }
        return request;
    }

    public async Task<PublicRequestContext> GetContextAsync(Guid requestId, ExternalAction action, string? token,
        CancellationToken cancellationToken)
    {
        var request = await ValidateAsync(requestId, action, token, false, cancellationToken);
        var correction = request.UploadStatus == DocumentRequestStatus.CorrectionsRequested
            ? await db.DocumentSubmissions.AsNoTracking().Where(submission => submission.ExternalRequestId == requestId &&
                    submission.Decision == SubmissionDecision.CorrectionsRequested)
                .OrderByDescending(submission => submission.DocumentVersion.VersionNumber)
                .Select(submission => submission.CorrectionExplanation).FirstOrDefaultAsync(cancellationToken)
            : null;
        var sourceHash = action == ExternalAction.Sign
            ? await db.DocumentVersions.Where(version => version.Id == request.DocumentVersionId)
                .Select(version => version.Sha256).SingleAsync(cancellationToken)
            : null;
        return new PublicRequestContext(request.Id, request.Action, request.Title, request.Progress,
            request.ExpiresAtUtc, request.Action == ExternalAction.Upload ? CanUpload(request) : request.Progress != RequestProgress.Signed,
            request.Action == ExternalAction.Sign, request.RecipientName, request.Instructions, request.UploadStatus, correction,
            request.SigningConsentText, sourceHash,
            action == ExternalAction.Sign ? request.CreatedAtUtc : null, request.ViewedAtUtc, request.StartedAtUtc,
            action == ExternalAction.Sign ? request.CompletedAtUtc : null);
    }

    public async Task<DocumentFile> OpenPdfAsync(Guid requestId, string? token, CancellationToken cancellationToken)
    {
        var request = await ValidateAsync(requestId, ExternalAction.Sign, token, false, cancellationToken);
        var versionId = request.Progress == RequestProgress.Signed
            ? request.SignedDocumentVersionId : request.DocumentVersionId;
        var version = await db.DocumentVersions.AsNoTracking().Where(item => item.Id == versionId &&
                item.Document.MatterId == request.MatterId)
            .Select(item => new { item.DocumentId, item.VersionNumber }).SingleOrDefaultAsync(cancellationToken)
            ?? throw Unavailable();
        return await documents.OpenFileAsync(request.MatterId, version.DocumentId, version.VersionNumber, cancellationToken);
    }

    private async Task<ExternalRequestDto> RotateAndSendAsync(ExternalRequest request, EmailTemplate template,
        CancellationToken cancellationToken, DateTimeOffset? expiresAtUtc = null)
    {
        var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var attempt = new EmailDeliveryAttempt
        {
            Id = Guid.NewGuid(), ExternalRequest = request, Template = template,
            StartedAtUtc = DateTimeOffset.UtcNow, Status = DeliveryStatus.Pending
        };
        // Reserve the unique document slot, including replacement, in the same transaction as the
        // invitation. Commit before SMTP; no transaction is held during network delivery.
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            if (request.Action == ExternalAction.Sign)
            {
                var source = await SigningWorkflow.LockSourceAsync(db, request.MatterId, request.DocumentVersionId!.Value, cancellationToken);
                await documents.EnsureSignableAsync(request.MatterId, source.Id, cancellationToken);
                if (db.Entry(request).State == EntityState.Added)
                {
                    await SigningWorkflow.ReserveAsync(db, request, source, cancellationToken);
                    activities.Record(request.Matter, MatterActivityType.SigningSent, $"Signing request #{request.Id} created.");
                    notifications.RecordSigning(request, source, $"Signing invitation created: {request.Title}.");
                }
                else
                {
                    // A resend may wait behind signing or replacement. Check the persisted workflow
                    // after that wait, before extending expiry or clearing revocation.
                    await db.Entry(request).ReloadAsync(cancellationToken);
                    await SigningWorkflow.EnsureCurrentAsync(db, request, source.DocumentId, cancellationToken);
                    if (request.Progress != RequestProgress.Signed && !SigningWorkflow.IsActive(request))
                        throw new ConflictException("Replace expired or revoked unsigned workflows with a new invitation.");
                }
            }
            var now = DateTimeOffset.UtcNow;
            if (expiresAtUtc <= now)
                throw new FluentValidation.ValidationException(
                    [new ValidationFailure("ExpiresAtUtc", "Expiry must be in the future.")]);
            if (db.Entry(request).State != EntityState.Added)
                activities.Record(request.Matter, MatterActivityType.ExternalRequestResent,
                    $"External request #{request.Id} reissued.");
            else if (request.Action == ExternalAction.Upload)
                activities.Record(request.Matter, MatterActivityType.DocumentRequested,
                    $"Document request #{request.Id} created.");
            request.TokenHash = Convert.ToHexStringLower(Hash(token));
            request.ExpiresAtUtc = expiresAtUtc?.ToUniversalTime() ?? now.AddDays(options.Value.ExpiryDays);
            request.RevokedAtUtc = null;
            db.EmailDeliveryAttempts.Add(attempt);
            await SaveAsync(request, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        // Finish this bounded synchronous attempt even if the HTTP caller disconnects after commit.
        using var sendTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            var actionPath = request.Action == ExternalAction.Upload ? "upload" : "sign";
            var url = $"{options.Value.FrontendBaseUrl.TrimEnd('/')}/external/{actionPath}/{request.Id}#token={token}";
            var (subject, body) = ExternalEmailTemplates.Render(template, request.Title, url);
            await email.SendAsync(request.RecipientEmail, subject, body, sendTimeout.Token);
            attempt.Status = DeliveryStatus.Sent;
        }
        catch (Exception exception)
        {
            // SMTP responses and exception messages can contain the full email and token.
            // Persist an allowlisted code only, and never pass the exception to a logger.
            attempt.Status = DeliveryStatus.Failed;
            attempt.FailureCode = exception is OperationCanceledException ? "SmtpTimeout" : "SmtpDeliveryFailed";
        }
        attempt.FinishedAtUtc = DateTimeOffset.UtcNow;
        using var saveTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await db.SaveChangesAsync(saveTimeout.Token);
        return await GetAsync(request.MatterId, request.Id, saveTimeout.Token);
    }

    private async Task<ExternalRequest> FindAsync(int matterId, Guid requestId, CancellationToken cancellationToken) =>
        await db.ExternalRequests.Include(item => item.Matter)
            .SingleOrDefaultAsync(item => item.MatterId == matterId && item.Id == requestId, cancellationToken)
        ?? throw new ResourceNotFoundException("Request not found.");

    private async Task SaveAsync(ExternalRequest request, CancellationToken cancellationToken)
    {
        // Serialize link/workflow changes with archive via the existing matter xmin concurrency token.
        request.Matter.UpdatedAtUtc = DateTimeOffset.UtcNow;
        db.Entry(request.Matter).Property(item => item.UpdatedAtUtc).IsModified = true;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The matter or request changed. Reload it and try again.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException
            { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_ExternalRequests_SigningDocumentId" })
        {
            throw new ConflictException("A signing workflow already exists for this document.");
        }
    }

    private void EnsureSendCanCommit()
    {
        // Check before tracking/mutating anything: SaveChanges cannot commit a caller-owned transaction.
        if (db.Database.CurrentTransaction is not null || System.Transactions.Transaction.Current is not null)
            throw new ConflictException("Send external requests outside an open database transaction.");
    }

    private static ExternalRequestDto ToDto(ExternalRequest request) =>
        new(request.Id, request.MatterId, request.Action, request.RecipientEmail,
            request.Title, request.Progress, request.ExpiresAtUtc, request.RevokedAtUtc, request.CompletedAtUtc,
            request.DeliveryAttempts.OrderByDescending(attempt => attempt.StartedAtUtc).ThenBy(attempt => attempt.Id)
                .Select(attempt => new DeliveryAttemptDto(attempt.Id, attempt.Template, attempt.Status,
                    attempt.StartedAtUtc, attempt.FinishedAtUtc, attempt.FailureCode)).ToArray(),
            request.RecipientName, request.Instructions, request.UploadStatus, request.UploadedDocumentId,
            request.Submissions.OrderByDescending(submission => submission.DocumentVersion.VersionNumber)
                .Select(submission => new DocumentSubmissionDto(submission.DocumentVersionId, submission.DocumentVersion.DocumentId,
                    submission.DocumentVersion.VersionNumber, submission.DocumentVersion.OriginalFileName,
                    submission.DocumentVersion.UploadedAtUtc, submission.Decision, submission.CorrectionExplanation,
                    submission.ReviewerId, submission.ReviewedAtUtc)).ToArray(), request.DocumentVersionId,
            request.SignerKind, request.ViewedAtUtc, request.StartedAtUtc, request.ReplacedAtUtc, request.SigningDocumentId);

    private static bool CanUpload(ExternalRequest request) => request.UploadStatus is
        DocumentRequestStatus.AwaitingUpload or DocumentRequestStatus.CorrectionsRequested;

    private static EmailTemplate InvitationFor(ExternalAction action) => action == ExternalAction.Upload
        ? EmailTemplate.DocumentRequest : EmailTemplate.SignatureInvitation;
    private static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
    private static ResourceNotFoundException Unavailable() => new("External link unavailable.");
}
