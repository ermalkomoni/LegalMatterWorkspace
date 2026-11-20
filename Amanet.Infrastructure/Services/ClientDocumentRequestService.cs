using Amanet.Application.Exceptions;
using Amanet.Application.Services;
using Amanet.Core.Entities;
using Amanet.Core.Enums;
using Amanet.Infrastructure.Data;
using Amanet.Infrastructure.Storage;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Amanet.Infrastructure.Services;

public sealed class ClientDocumentRequestService(ApplicationDbContext db, IExternalRequestService links,
    PrivateDocumentStorage storage, IMatterActivityService activities, IAdminNotificationService notifications)
    : IClientDocumentRequestService
{
    public async Task<PublicRequestContext> UploadAsync(Guid requestId, string? token, Stream content, string fileName,
        CancellationToken cancellationToken)
    {
        EnsureCanCommit();
        var validated = await links.ValidateAsync(requestId, ExternalAction.Upload, token, true, cancellationToken);
        var request = await FindAsync(validated.MatterId, requestId, cancellationToken);
        EnsureCurrent(request, validated);
        var displayName = Path.GetFileName(fileName.Replace('\\', '/'));
        displayName = string.Concat(displayName.Where(character => !char.IsControl(character))).Trim();
        if (displayName.Length is 0 or > 255)
            throw new InvalidDocumentException("The PDF filename must contain between 1 and 255 characters.");

        var stored = await storage.SaveAsync(content, cancellationToken);
        try
        {
            // Validation is repeated after file I/O; expiry/revocation may have changed during the upload.
            validated = await links.ValidateAsync(requestId, ExternalAction.Upload, token, true, cancellationToken);
            EnsureCurrent(request, validated);
            var document = request.UploadedDocument;
            if (document is not null && document.MatterId != request.MatterId)
                throw new ResourceNotFoundException("Document not found.");
            if (document is null)
            {
                document = new MatterDocument
                {
                    Matter = request.Matter, Title = request.Title, CreatedAtUtc = DateTimeOffset.UtcNow,
                    Classification = DocumentClassification.ReviewPending
                };
                request.UploadedDocument = document;
            }
            var number = request.UploadedDocumentId.HasValue
                ? (await db.DocumentVersions.Where(version => version.DocumentId == request.UploadedDocumentId)
                    .MaxAsync(version => (int?)version.VersionNumber, cancellationToken) ?? 0) + 1
                : 1;
            var version = new DocumentVersion
            {
                Document = document, VersionNumber = number, OriginalFileName = displayName,
                ByteLength = stored.ByteLength, Sha256 = stored.Sha256, StorageKey = stored.Key,
                Source = "ClientUpload", UploaderId = null, UploadedAtUtc = DateTimeOffset.UtcNow
            };
            db.DocumentSubmissions.Add(new DocumentSubmission { ExternalRequest = request, DocumentVersion = version });
            request.UploadStatus = DocumentRequestStatus.Submitted;
            document.Classification = DocumentClassification.ReviewPending;
            document.UpdatedAtUtc = version.UploadedAtUtc;
            activities.Record(request.Matter, MatterActivityType.ClientDocumentSubmitted,
                $"Request #{request.Id}: PDF version {number} submitted for review.");
            notifications.RecordSubmission(request, version);
            await SaveAsync(request, cancellationToken);
        }
        catch
        {
            storage.Delete(stored.Key);
            throw;
        }
        // Return the committed receipt. Revalidating here can report failure after a successful upload
        // if a link expires/is revoked or the database becomes unavailable immediately after commit.
        return new PublicRequestContext(request.Id, ExternalAction.Upload, request.Title, request.Progress,
            request.ExpiresAtUtc, false, false, request.RecipientName, request.Instructions, request.UploadStatus, null);
    }

    public async Task<ExternalRequestDto> ReviewAsync(int matterId, Guid requestId, int versionId,
        ReviewSubmissionRequest input, int reviewerId, CancellationToken cancellationToken)
    {
        EnsureCanCommit();
        if (!input.Decision.HasValue || !Enum.IsDefined(input.Decision.Value))
            throw new ValidationException([new ValidationFailure("Decision", "Choose acceptance or corrections.")]);
        if (input.Decision == SubmissionDecision.CorrectionsRequested && string.IsNullOrWhiteSpace(input.Explanation))
            throw new ValidationException([new ValidationFailure("Explanation", "Explain the required correction.")]);
        if (input.Explanation?.Length > 5000)
            throw new ValidationException([new ValidationFailure("Explanation", "Use at most 5000 characters.")]);
        var request = await FindAsync(matterId, requestId, cancellationToken);
        MatterRules.EnsureWritable(request.Matter);
        if (!await db.Users.AnyAsync(user => user.Id == reviewerId, cancellationToken))
            throw new UnauthorizedAccessException();
        var submission = await db.DocumentSubmissions.Include(item => item.DocumentVersion)
            .SingleOrDefaultAsync(item => item.ExternalRequestId == requestId && item.DocumentVersionId == versionId &&
                item.Decision == null &&
                item.DocumentVersion.DocumentId == request.UploadedDocumentId && item.DocumentVersion.Document.MatterId == matterId,
                cancellationToken) ?? throw new ResourceNotFoundException("Submission not found.");
        if (request.UploadStatus != DocumentRequestStatus.Submitted || submission.Decision.HasValue)
            throw new ConflictException("Only the current unreviewed submission can be reviewed.");

        submission.Decision = input.Decision;
        submission.ReviewerId = reviewerId;
        submission.ReviewedAtUtc = DateTimeOffset.UtcNow;
        var document = request.UploadedDocument!;
        document.UpdatedAtUtc = submission.ReviewedAtUtc.Value;
        if (input.Decision == SubmissionDecision.CorrectionsRequested)
        {
            submission.CorrectionExplanation = input.Explanation!.Trim();
            request.UploadStatus = DocumentRequestStatus.CorrectionsRequested;
            document.Classification = DocumentClassification.ReviewPending;
            activities.Record(request.Matter, MatterActivityType.CorrectionsRequested,
                $"Request #{request.Id}: corrections requested for version {submission.DocumentVersion.VersionNumber}.");
            // The shared sender commits the review, state, activity and new token together before sending.
            return await links.ResendAsync(matterId, requestId, EmailTemplate.Correction, cancellationToken);
        }

        request.UploadStatus = DocumentRequestStatus.Accepted;
        request.Progress = RequestProgress.Completed;
        request.CompletedAtUtc = submission.ReviewedAtUtc;
        document.Classification = DocumentClassification.Accepted;
        activities.Record(request.Matter, MatterActivityType.SubmissionAccepted,
            $"Request #{request.Id}: version {submission.DocumentVersion.VersionNumber} accepted.");
        await SaveAsync(request, cancellationToken);
        return await links.GetAsync(matterId, requestId, cancellationToken);
    }

    private async Task<ExternalRequest> FindAsync(int matterId, Guid requestId, CancellationToken cancellationToken)
    {
        return await db.ExternalRequests.Include(request => request.Matter).Include(request => request.UploadedDocument)
            .SingleOrDefaultAsync(request => request.Id == requestId && request.MatterId == matterId &&
                request.Action == ExternalAction.Upload, cancellationToken)
            ?? throw new ResourceNotFoundException("Document request not found.");
    }

    private void EnsureCurrent(ExternalRequest tracked, ExternalRequest validated)
    {
        if (db.Entry(tracked).Property(request => request.Version).OriginalValue != validated.Version ||
            db.Entry(tracked.Matter).Property(matter => matter.Version).OriginalValue != validated.Matter.Version)
            throw new ConflictException("The request or matter changed. Reload it and try again.");
    }

    private void EnsureCanCommit()
    {
        // Reject before tracking workflow changes or writing a file: neither a file promotion nor
        // an email send can participate in a caller's later database rollback.
        if (db.Database.CurrentTransaction is not null || System.Transactions.Transaction.Current is not null)
            throw new ConflictException("Submit and review documents outside an open database transaction.");
    }

    private async Task SaveAsync(ExternalRequest request, CancellationToken cancellationToken)
    {
        request.Matter.UpdatedAtUtc = DateTimeOffset.UtcNow;
        db.Entry(request.Matter).Property(matter => matter.UpdatedAtUtc).IsModified = true;
        try
        {
            // EF saves document/version, request, submission, activity and notification in one transaction.
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The request or matter changed. Reload it and try again.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_DocumentVersions_DocumentId_VersionNumber" })
        {
            throw new ConflictException("A submission was saved concurrently. Reload the request and try again.");
        }
    }
}
