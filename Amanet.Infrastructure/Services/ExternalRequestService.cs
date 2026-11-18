using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Amanet.Application.DTOs.Documents;
using Amanet.Application.Exceptions;
using Amanet.Application.Services;
using Amanet.Core.Entities;
using Amanet.Infrastructure.Data;
using Amanet.Infrastructure.Email;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Amanet.Infrastructure.Services;

public sealed class ExternalRequestService(ApplicationDbContext db, IEmailSender email,
    IOptions<ExternalLinkOptions> options, IDocumentService documents) : IExternalRequestService
{
    public async Task<IReadOnlyList<ExternalRequestDto>> ListAsync(int matterId, CancellationToken cancellationToken)
    {
        if (!await db.Matters.AnyAsync(matter => matter.Id == matterId, cancellationToken))
            throw new ResourceNotFoundException("Matter not found.");
        var requests = await db.ExternalRequests.AsNoTracking().Include(request => request.DeliveryAttempts)
            .Where(request => request.MatterId == matterId).OrderByDescending(request => request.CreatedAtUtc)
            .ThenBy(request => request.Id).ToListAsync(cancellationToken);
        return requests.Select(ToDto).ToArray();
    }

    public async Task<ExternalRequestDto> GetAsync(int matterId, Guid requestId, CancellationToken cancellationToken)
    {
        var request = await db.ExternalRequests.AsNoTracking().Include(item => item.DeliveryAttempts)
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

        var matter = await db.Matters.SingleOrDefaultAsync(item => item.Id == matterId, cancellationToken)
            ?? throw new ResourceNotFoundException("Matter not found.");
        MatterRules.EnsureWritable(matter);
        if (input.DocumentVersionId.HasValue && !await db.DocumentVersions.AnyAsync(version =>
                version.Id == input.DocumentVersionId && version.Document.MatterId == matterId, cancellationToken))
            throw new ResourceNotFoundException("Document version not found.");

        var request = new ExternalRequest
        {
            Id = Guid.NewGuid(), Matter = matter, Action = input.Action,
            RecipientEmail = input.RecipientEmail.Trim(), Title = input.Title.Trim(),
            DocumentVersionId = input.DocumentVersionId, CreatedAtUtc = DateTimeOffset.UtcNow
        };
        db.ExternalRequests.Add(request);
        return await RotateAndSendAsync(request, InvitationFor(request.Action), cancellationToken);
    }

    public async Task<ExternalRequestDto> ResendAsync(int matterId, Guid requestId, EmailTemplate? template,
        CancellationToken cancellationToken)
    {
        EnsureSendCanCommit();
        var request = await FindAsync(matterId, requestId, cancellationToken);
        MatterRules.EnsureWritable(request.Matter);
        if (request.Action == ExternalAction.Upload && request.Progress == RequestProgress.Completed)
            throw new ConflictException("Completed upload requests cannot be resent.");
        var selectedTemplate = template ?? InvitationFor(request.Action);
        if (!Enum.IsDefined(selectedTemplate) ||
            (selectedTemplate == EmailTemplate.DocumentRequest && request.Action != ExternalAction.Upload) ||
            (selectedTemplate == EmailTemplate.SignatureInvitation && request.Action != ExternalAction.Sign) ||
            (selectedTemplate == EmailTemplate.Correction && request.Action != ExternalAction.Upload))
            throw new FluentValidation.ValidationException(
                [new ValidationFailure("Template", "The email template does not match the request action.")]);
        return await RotateAndSendAsync(request, selectedTemplate, cancellationToken);
    }

    public async Task<ExternalRequestDto> RevokeAsync(int matterId, Guid requestId, CancellationToken cancellationToken)
    {
        // Revoke the current link atomically, even if this context tracked an older revoked link.
        await db.ExternalRequests.Where(request => request.MatterId == matterId && request.Id == requestId &&
                request.RevokedAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(request => request.RevokedAtUtc, DateTimeOffset.UtcNow), cancellationToken);
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
            request.ExpiresAtUtc <= DateTimeOffset.UtcNow ||
            !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(request.TokenHash), Hash(token)))
            throw Unavailable();
        if (forSubmission && request.Progress == RequestProgress.Completed)
            throw new ConflictException("This request has already been completed.");
        return request;
    }

    public async Task<PublicRequestContext> GetContextAsync(Guid requestId, ExternalAction action, string? token,
        CancellationToken cancellationToken)
    {
        var request = await ValidateAsync(requestId, action, token, false, cancellationToken);
        return new PublicRequestContext(request.Id, request.Action, request.Title, request.Progress,
            request.ExpiresAtUtc, request.Progress != RequestProgress.Completed, request.Action == ExternalAction.Sign);
    }

    public async Task<DocumentFile> OpenPdfAsync(Guid requestId, string? token, CancellationToken cancellationToken)
    {
        var request = await ValidateAsync(requestId, ExternalAction.Sign, token, false, cancellationToken);
        var versionId = request.Progress == RequestProgress.Completed
            ? request.SignedDocumentVersionId : request.DocumentVersionId;
        var version = await db.DocumentVersions.AsNoTracking().Where(item => item.Id == versionId &&
                item.Document.MatterId == request.MatterId)
            .Select(item => new { item.DocumentId, item.VersionNumber }).SingleOrDefaultAsync(cancellationToken)
            ?? throw Unavailable();
        return await documents.OpenFileAsync(request.MatterId, version.DocumentId, version.VersionNumber, cancellationToken);
    }

    public async Task CompleteAsync(Guid requestId, ExternalAction action, string? token, int? signedDocumentVersionId,
        CancellationToken cancellationToken)
    {
        var validated = await ValidateAsync(requestId, action, token, true, cancellationToken);
        var request = await FindAsync(validated.MatterId, requestId, cancellationToken);
        // Preserve tracked workflow changes, but never complete using an older request/matter version.
        if (db.Entry(request).Property(item => item.Version).OriginalValue != validated.Version ||
            db.Entry(request.Matter).Property(item => item.Version).OriginalValue != validated.Matter.Version)
            throw new ConflictException("The matter or request changed. Reload it and try again.");
        if (action == ExternalAction.Sign)
        {
            var originalDocumentId = await db.DocumentVersions.Where(version => version.Id == request.DocumentVersionId)
                .Select(version => version.DocumentId).SingleAsync(cancellationToken);
            if (signedDocumentVersionId == request.DocumentVersionId || !await db.DocumentVersions.AnyAsync(version =>
                    version.Id == signedDocumentVersionId && version.DocumentId == originalDocumentId &&
                    version.Document.MatterId == request.MatterId, cancellationToken))
                throw new ResourceNotFoundException("Signed document version not found.");
        }
        else if (signedDocumentVersionId.HasValue)
            throw new ConflictException("Upload requests do not have a signed PDF.");

        request.Progress = RequestProgress.Completed;
        request.CompletedAtUtc = DateTimeOffset.UtcNow;
        request.SignedDocumentVersionId = signedDocumentVersionId;
        // Also saves workflow changes tracked in this scoped DbContext, in one EF transaction.
        await SaveAsync(request, cancellationToken);
    }

    private async Task<ExternalRequestDto> RotateAndSendAsync(ExternalRequest request, EmailTemplate template,
        CancellationToken cancellationToken)
    {
        var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        request.TokenHash = Convert.ToHexStringLower(Hash(token));
        request.ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(options.Value.ExpiryDays);
        request.RevokedAtUtc = null;
        var attempt = new EmailDeliveryAttempt
        {
            Id = Guid.NewGuid(), ExternalRequest = request, Template = template,
            StartedAtUtc = DateTimeOffset.UtcNow, Status = DeliveryStatus.Pending
        };
        db.EmailDeliveryAttempts.Add(attempt);
        // Commit request, rotated link and pending attempt BEFORE contacting SMTP.
        await SaveAsync(request, cancellationToken);

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
                    attempt.StartedAtUtc, attempt.FinishedAtUtc, attempt.FailureCode)).ToArray());

    private static EmailTemplate InvitationFor(ExternalAction action) => action == ExternalAction.Upload
        ? EmailTemplate.DocumentRequest : EmailTemplate.SignatureInvitation;
    private static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
    private static ResourceNotFoundException Unavailable() => new("External link unavailable.");
}
