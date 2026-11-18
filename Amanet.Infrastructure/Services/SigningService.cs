using System.Security.Cryptography;
using Amanet.Application.Exceptions;
using Amanet.Application.Services;
using Amanet.Core.Entities;
using Amanet.Core.Enums;
using Amanet.Infrastructure.Data;
using Amanet.Infrastructure.Email;
using Amanet.Infrastructure.Pdf;
using Amanet.Infrastructure.Storage;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Amanet.Infrastructure.Services;

public sealed class SigningService(ApplicationDbContext db, IExternalRequestService links, IDocumentService documents,
    PrivateDocumentStorage storage, PdfSigningGenerator generator, IMatterActivityService activities,
    IAdminNotificationService notifications, IOptions<ExternalLinkOptions> options) : ISigningService
{
    public async Task<SignatureDto> SignAdminAsync(int matterId, int documentId, AdminSignRequest input, int adminId,
        CancellationToken cancellationToken)
    {
        EnsureCanCommit();
        var name = ValidateInput(input);
        var admin = await db.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == adminId, cancellationToken);
        if (admin is null || string.IsNullOrWhiteSpace(admin.Email))
            throw new UnauthorizedAccessException();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var source = await SigningWorkflow.LockSourceAsync(db, matterId, input.SourceVersionId, cancellationToken);
        if (source.DocumentId != documentId)
            throw new ResourceNotFoundException("Source version not found.");
        MatterRules.EnsureWritable(source.Document.Matter);
        var existing = await RecordQuery().SingleOrDefaultAsync(record => record.DocumentId == documentId, cancellationToken);
        if (existing is not null)
        {
            ValidateRepeat(existing, name, SignerKind.Admin, adminId, input.SourceVersionId);
            return ToDto(existing);
        }
        await documents.EnsureSignableAsync(matterId, source.Id, cancellationToken);
        var request = new ExternalRequest
        {
            Id = Guid.NewGuid(), Matter = source.Document.Matter, Action = ExternalAction.Sign,
            DocumentVersionId = source.Id, SignerKind = SignerKind.Admin, AdminSignerId = adminId,
            SigningConsentText = SigningConsent.Text,
            RecipientEmail = admin.Email, RecipientName = name, Title = source.Document.Title,
            CreatedAtUtc = DateTimeOffset.UtcNow, ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(options.Value.ExpiryDays),
            Progress = RequestProgress.Sent,
            TokenHash = Convert.ToHexStringLower(SHA256.HashData(RandomNumberGenerator.GetBytes(32)))
        };
        await SigningWorkflow.ReserveAsync(db, request, source, cancellationToken);
        db.ExternalRequests.Add(request);
        RecordTransition(request, source, MatterActivityType.SigningSent, "Signing request created");
        return await FinalizeAsync(request, source, name, admin.Email, adminId, transaction, cancellationToken);
    }

    public async Task<SignatureDto> SignClientAsync(Guid requestId, string? token, SignRequest input,
        CancellationToken cancellationToken)
    {
        EnsureCanCommit();
        var name = ValidateInput(input);
        var snapshot = await links.ValidateAsync(requestId, ExternalAction.Sign, token, false, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var source = await SigningWorkflow.LockSourceAsync(db, snapshot.MatterId, snapshot.DocumentVersionId!.Value, cancellationToken);
        var request = await LoadClientAsync(requestId, token, cancellationToken);
        return await FinalizeAsync(request, source, name, request.RecipientEmail, null, transaction, cancellationToken);
    }

    public async Task<PublicRequestContext> AdvanceAsync(Guid requestId, string? token, RequestProgress progress,
        CancellationToken cancellationToken)
    {
        EnsureCanCommit();
        if (progress is not (RequestProgress.Viewed or RequestProgress.Started))
            throw new ValidationException([new ValidationFailure("Progress", "Choose Viewed or Started.")]);
        var snapshot = await links.ValidateAsync(requestId, ExternalAction.Sign, token, false, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var source = await SigningWorkflow.LockSourceAsync(db, snapshot.MatterId, snapshot.DocumentVersionId!.Value, cancellationToken);
        var request = await LoadClientAsync(requestId, token, cancellationToken);
        if (request.Progress != RequestProgress.Signed && Advance(request, source, progress, Timestamp(request)))
        {
            TouchMatter(request.Matter);
            await SaveAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new PublicRequestContext(request.Id, ExternalAction.Sign, request.Title, request.Progress,
            request.ExpiresAtUtc, request.Progress != RequestProgress.Signed, true, request.RecipientName, request.Instructions,
            null, null, request.SigningConsentText, source.Sha256, request.CreatedAtUtc, request.ViewedAtUtc, request.StartedAtUtc, request.CompletedAtUtc);
    }

    public async Task<SignatureDto> GetAsync(int matterId, int documentId, CancellationToken cancellationToken)
    {
        var record = await RecordQuery().SingleOrDefaultAsync(record => record.DocumentId == documentId &&
                record.Document.MatterId == matterId, cancellationToken)
            ?? throw new ResourceNotFoundException("Signed record not found.");
        return ToDto(record);
    }

    private async Task<SignatureDto> FinalizeAsync(ExternalRequest request, DocumentVersion source, string name,
        string email, int? adminId, IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        await SigningWorkflow.EnsureCurrentAsync(db, request, source.DocumentId, cancellationToken,
            allowUnsaved: db.Entry(request).State == EntityState.Added);
        var existing = await RecordQuery().SingleOrDefaultAsync(record => record.DocumentId == source.DocumentId, cancellationToken);
        if (existing is not null)
        {
            if (existing.RequestId != request.Id)
                throw new ConflictException("This document was signed through another workflow.");
            ValidateRepeat(existing, name, request.SignerKind!.Value, adminId, source.Id);
            return ToDto(existing);
        }
        MatterRules.EnsureWritable(source.Document.Matter);
        if (source.Document.SignedAtUtc.HasValue || request.Progress == RequestProgress.Signed)
            throw new ConflictException("This document is already signed.");
        if (!SigningWorkflow.IsActive(request))
            throw new ConflictException("This signing workflow is unavailable.");
        await documents.EnsureSignableAsync(request.MatterId, source.Id, cancellationToken);

        var audit = new SignedDocument
        {
            RequestId = request.Id, Request = request, Document = source.Document,
            DocumentId = source.DocumentId, SourceVersionId = source.Id, SourceVersion = source,
            SignerKind = request.SignerKind!.Value, SignerName = name,
            SignerEmail = email, AdminSignerId = adminId, SignedAtUtc = Timestamp(request),
            ConsentText = request.SigningConsentText!, SourceSha256 = source.Sha256
        };
        StoredPdf? staged = null;
        var promoted = false;
        var commitAttempted = false;
        try
        {
            // The document row lock is the finalization claim. Other submissions wait, then return
            // the completed record; no lease, queue or persistent Finalizing state is needed.
            staged = await generator.GenerateAsync(source, audit, cancellationToken);
            if (request.ExpiresAtUtc <= DateTimeOffset.UtcNow)
                throw new ConflictException("The signing workflow expired during PDF generation.");
            var number = await db.DocumentVersions.Where(version => version.DocumentId == source.DocumentId)
                .MaxAsync(version => version.VersionNumber, cancellationToken) + 1;
            var final = new DocumentVersion
            {
                Document = source.Document, VersionNumber = number, OriginalFileName = $"signed-{source.DocumentId}.pdf",
                ByteLength = staged.ByteLength, Sha256 = staged.Sha256, StorageKey = staged.Key,
                Source = "TypedSignature", UploaderId = adminId, UploadedAtUtc = audit.SignedAtUtc
            };
            audit.FinalVersion = final;
            Advance(request, source, RequestProgress.Started, audit.SignedAtUtc);
            request.Progress = RequestProgress.Signed;
            request.CompletedAtUtc = audit.SignedAtUtc;
            request.SignedDocumentVersion = final;
            source.Document.SignedAtUtc = audit.SignedAtUtc;
            source.Document.UpdatedAtUtc = audit.SignedAtUtc;
            db.SignedDocuments.Add(audit);
            RecordTransition(request, final, MatterActivityType.DocumentSigned, "Document signed");
            TouchMatter(request.Matter);
            await SaveAsync(cancellationToken);
            storage.Promote(staged);
            promoted = true;
            cancellationToken.ThrowIfCancellationRequested();
            commitAttempted = true;
            await transaction.CommitAsync(cancellationToken);
            return ToDto(audit);
        }
        finally
        {
            if (staged is not null)
            {
                storage.DeleteStaged(staged.Key);
                // A failed commit acknowledgement is ambiguous. Retain a published file rather than
                // deleting bytes that a committed signed record may reference; reconciliation is manual.
                if (promoted && !commitAttempted)
                    storage.Delete(staged.Key);
            }
        }
    }

    private async Task<ExternalRequest> LoadClientAsync(Guid requestId, string? token, CancellationToken cancellationToken)
    {
        var request = await db.ExternalRequests.Include(item => item.Matter).SingleAsync(item => item.Id == requestId, cancellationToken);
        // This service owns its unit of work. Refresh after acquiring the document lock so a waiting
        // simultaneous submission observes the winner instead of a previously tracked snapshot.
        await db.Entry(request).ReloadAsync(cancellationToken);
        await db.Entry(request.Matter).ReloadAsync(cancellationToken);
        var current = await links.ValidateAsync(requestId, ExternalAction.Sign, token, false, cancellationToken);
        if (request.Version != current.Version || request.Matter.Version != current.Matter.Version)
            throw new ConflictException("The request or matter changed. Try again.");
        return request;
    }

    private bool Advance(ExternalRequest request, DocumentVersion source, RequestProgress progress, DateTimeOffset now)
    {
        var changed = false;
        if (!request.ViewedAtUtc.HasValue)
        {
            request.ViewedAtUtc = now;
            RecordTransition(request, source, MatterActivityType.SigningViewed, "Signing document viewed");
            changed = true;
        }
        if (progress == RequestProgress.Started && !request.StartedAtUtc.HasValue)
        {
            request.StartedAtUtc = now;
            RecordTransition(request, source, MatterActivityType.SigningStarted, "Signing form started");
            changed = true;
        }
        request.Progress = request.StartedAtUtc.HasValue ? RequestProgress.Started : RequestProgress.Viewed;
        return changed;
    }

    private void RecordTransition(ExternalRequest request, DocumentVersion version, MatterActivityType type, string text)
    {
        activities.Record(request.Matter, type, $"{text}: request #{request.Id}.");
        notifications.RecordSigning(request, version, $"{text}: {request.Title}.");
    }

    private IQueryable<SignedDocument> RecordQuery()
    {
        return db.SignedDocuments.AsNoTracking().Include(record => record.FinalVersion);
    }

    private void TouchMatter(Matter matter)
    {
        matter.UpdatedAtUtc = DateTimeOffset.UtcNow;
        db.Entry(matter).Property(item => item.UpdatedAtUtc).IsModified = true;
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The matter or workflow changed during signing. Try again.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres &&
            (postgres.SqlState is PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure ||
             (postgres.SqlState == PostgresErrorCodes.UniqueViolation && postgres.ConstraintName is
                 "IX_SignedDocuments_DocumentId" or "PK_SignedDocuments" or "IX_ExternalRequests_SigningDocumentId" or
                 "IX_DocumentVersions_DocumentId_VersionNumber")))
        {
            throw new ConflictException("Another document operation completed concurrently. Try again.");
        }
    }

    private void EnsureCanCommit()
    {
        if (db.Database.CurrentTransaction is not null || System.Transactions.Transaction.Current is not null)
            throw new ConflictException("Signing operations must own their database transaction.");
    }

    private static string ValidateInput(SignRequest input)
    {
        if (string.IsNullOrWhiteSpace(input.TypedName) || input.TypedName.Length > 200 || input.TypedName.Any(char.IsControl))
            throw new ValidationException([new ValidationFailure("TypedName", "Enter a name of at most 200 characters without control characters.")]);
        if (input.ConsentAccepted != true)
            throw new ValidationException([new ValidationFailure("ConsentAccepted", "Explicit consent is required to sign.")]);
        return input.TypedName.Trim();
    }

    private static void ValidateRepeat(SignedDocument record, string name, SignerKind kind, int? adminId, int sourceId)
    {
        if (record.SignerName != name || record.SignerKind != kind || record.AdminSignerId != adminId || record.SourceVersionId != sourceId)
            throw new ConflictException("The document is already signed using different signing details.");
    }

    private static DateTimeOffset Timestamp(ExternalRequest request)
    {
        var now = DateTimeOffset.UtcNow;
        if (request.CreatedAtUtc > now) now = request.CreatedAtUtc;
        if (request.ViewedAtUtc > now) now = request.ViewedAtUtc.Value;
        if (request.StartedAtUtc > now) now = request.StartedAtUtc.Value;
        return now;
    }

    private static SignatureDto ToDto(SignedDocument record)
    {
        return new SignatureDto(record.RequestId, record.DocumentId, record.SourceVersionId, record.SourceSha256,
            record.FinalVersionId, record.FinalVersion.VersionNumber, record.FinalVersion.OriginalFileName, record.FinalVersion.Sha256,
            record.SignerKind, record.SignerName, record.SignerEmail, record.AdminSignerId, record.SignedAtUtc, record.ConsentText);
    }
}

// Shared coordination for email invitations and both signing flows. The nullable unique slot is the
// database invariant; the document lock serializes reservation, progress and finalization.
internal static class SigningWorkflow
{
    internal static async Task<DocumentVersion> LockSourceAsync(ApplicationDbContext db, int matterId, int sourceId,
        CancellationToken cancellationToken)
    {
        var documentId = await db.DocumentVersions.AsNoTracking().Where(version => version.Id == sourceId &&
                version.Document.MatterId == matterId).Select(version => (int?)version.DocumentId).SingleOrDefaultAsync(cancellationToken)
            ?? throw new ResourceNotFoundException("Source version not found.");
        await db.MatterDocuments.FromSqlInterpolated($"SELECT * FROM \"MatterDocuments\" WHERE \"Id\" = {documentId} AND \"MatterId\" = {matterId} FOR NO KEY UPDATE")
            .AsNoTracking().SingleAsync(cancellationToken);
        var source = await db.DocumentVersions.Include(version => version.Document).ThenInclude(document => document.Matter)
            .SingleAsync(version => version.Id == sourceId, cancellationToken);
        await db.Entry(source.Document).ReloadAsync(cancellationToken);
        await db.Entry(source.Document.Matter).ReloadAsync(cancellationToken);
        MatterRules.EnsureWritable(source.Document.Matter);
        return source;
    }

    internal static async Task ReserveAsync(ApplicationDbContext db, ExternalRequest request, DocumentVersion source,
        CancellationToken cancellationToken)
    {
        if (source.Document.SignedAtUtc.HasValue)
            throw new ConflictException("Signed documents require a new logical document for another signing workflow.");
        var previous = await db.ExternalRequests.AsNoTracking().SingleOrDefaultAsync(item => item.SigningDocumentId == source.DocumentId, cancellationToken);
        if (previous is not null)
        {
            if (previous.Progress == RequestProgress.Signed || IsActive(previous))
                throw new ConflictException("A signing workflow already owns this document.");
            var now = DateTimeOffset.UtcNow;
            var released = await db.ExternalRequests.Where(item => item.Id == previous.Id && item.SigningDocumentId == source.DocumentId &&
                    item.Progress != RequestProgress.Signed && (item.RevokedAtUtc != null || item.ExpiresAtUtc <= now))
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.SigningDocumentId, (int?)null)
                    .SetProperty(item => item.ReplacedAtUtc, now).SetProperty(item => item.RevokedAtUtc, item => item.RevokedAtUtc ?? now), cancellationToken);
            if (released != 1)
                throw new ConflictException("The previous workflow changed. Try again.");
        }
        request.SigningDocumentId = source.DocumentId;
    }

    internal static async Task EnsureCurrentAsync(ApplicationDbContext db, ExternalRequest request, int documentId,
        CancellationToken cancellationToken, bool allowUnsaved = false)
    {
        if (request.ReplacedAtUtc.HasValue || request.SigningDocumentId != documentId ||
            (!allowUnsaved && !await db.ExternalRequests.AnyAsync(item => item.Id == request.Id &&
                item.SigningDocumentId == documentId && item.ReplacedAtUtc == null, cancellationToken)))
            throw new ConflictException("This signing workflow has been replaced.");
    }

    internal static bool IsActive(ExternalRequest request)
    {
        return request.ReplacedAtUtc is null && request.RevokedAtUtc is null && request.ExpiresAtUtc > DateTimeOffset.UtcNow;
    }
}
