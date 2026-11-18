using System.Linq.Expressions;
using Amanet.Application.DTOs;
using Amanet.Application.DTOs.Documents;
using Amanet.Application.Exceptions;
using Amanet.Application.Services;
using Amanet.Core.Entities;
using Amanet.Core.Enums;
using Amanet.Infrastructure.Data;
using Amanet.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Amanet.Infrastructure.Services;

public sealed class DocumentService(ApplicationDbContext db, PrivateDocumentStorage storage,
    IMatterActivityService activities) : IDocumentService
{
    private static readonly Expression<Func<MatterDocument, DocumentDto>> Projection = document =>
        new DocumentDto(document.Id, document.MatterId, document.Matter.ReferenceNumber, document.Title, document.Category,
            document.Versions.Max(version => version.VersionNumber), document.CreatedAtUtc, document.UpdatedAtUtc, document.Classification);

    public async Task<PagedResult<DocumentDto>> ListAsync(DocumentListQuery query, CancellationToken cancellationToken)
    {
        if (query.MatterId.HasValue && !await db.Matters.AnyAsync(matter => matter.Id == query.MatterId, cancellationToken))
            throw new ResourceNotFoundException("Matter not found.");

        var documents = db.MatterDocuments.AsNoTracking();
        if (query.MatterId.HasValue)
            documents = documents.Where(document => document.MatterId == query.MatterId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            documents = documents.Where(document => document.Title.ToLower().Contains(term) ||
                (document.Category != null && document.Category.ToLower().Contains(term)) ||
                document.Matter.Title.ToLower().Contains(term) || document.Matter.ReferenceNumber.ToLower().Contains(term) ||
                document.Versions.Any(version => version.OriginalFileName.ToLower().Contains(term)));
        }
        var totalCount = await documents.CountAsync(cancellationToken);
        var offset = ((long)query.Page - 1) * query.PageSize;
        var items = offset > int.MaxValue ? [] : await documents.OrderByDescending(document => document.UpdatedAtUtc)
            .ThenByDescending(document => document.Id).Skip((int)offset).Take(query.PageSize)
            .Select(Projection).ToListAsync(cancellationToken);
        return new PagedResult<DocumentDto>(items, totalCount, query.Page, query.PageSize);
    }

    public async Task<DocumentDto> GetAsync(int matterId, int documentId, CancellationToken cancellationToken)
    {
        return await db.MatterDocuments.AsNoTracking()
            .Where(document => document.MatterId == matterId && document.Id == documentId)
            .Select(Projection).SingleOrDefaultAsync(cancellationToken)
            ?? throw new ResourceNotFoundException("Document not found.");
    }

    public async Task<DocumentDto> UpdateAsync(int matterId, int documentId, DocumentRequest request, CancellationToken cancellationToken)
    {
        var document = await db.MatterDocuments.Include(document => document.Matter)
            .SingleOrDefaultAsync(document => document.MatterId == matterId && document.Id == documentId, cancellationToken)
            ?? throw new ResourceNotFoundException("Document not found.");
        MatterRules.EnsureWritable(document.Matter);
        var title = request.Title.Trim();
        var category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim();
        if (document.Title != title || document.Category != category)
        {
            document.Title = title;
            document.Category = category;
            document.UpdatedAtUtc = DateTimeOffset.UtcNow;
            activities.Record(document.Matter, MatterActivityType.DocumentUpdated, $"Document #{document.Id} details updated.");
            await SaveAsync(document.Matter, cancellationToken);
        }
        return await GetAsync(matterId, documentId, cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentVersionDto>> VersionsAsync(int matterId, int documentId, CancellationToken cancellationToken)
    {
        await GetAsync(matterId, documentId, cancellationToken);
        return await db.DocumentVersions.AsNoTracking().Where(version => version.DocumentId == documentId)
            .OrderByDescending(version => version.VersionNumber)
            .Select(version => new DocumentVersionDto(version.Id, version.DocumentId, version.VersionNumber,
                version.OriginalFileName, version.ContentType, version.ByteLength, version.Sha256,
                version.Source, version.UploaderId, version.UploadedAtUtc)).ToListAsync(cancellationToken);
    }

    public async Task<DocumentFile> OpenFileAsync(int matterId, int documentId, int versionNumber, CancellationToken cancellationToken)
    {
        var version = await db.DocumentVersions.AsNoTracking().SingleOrDefaultAsync(version =>
            version.DocumentId == documentId && version.Document.MatterId == matterId && version.VersionNumber == versionNumber,
            cancellationToken) ?? throw new ResourceNotFoundException("Document version not found.");
        return new DocumentFile(storage.OpenRead(version.StorageKey, version.ByteLength), version.OriginalFileName,
            version.Sha256, version.UploadedAtUtc);
    }

    public async Task EnsureSignableAsync(int matterId, int versionId, CancellationToken cancellationToken)
    {
        var version = await db.DocumentVersions.AsNoTracking().Where(version => version.Id == versionId &&
                version.Document.MatterId == matterId)
            .Select(version => new { version.Document.Classification, version.Document.Matter.ArchivedAtUtc })
            .SingleOrDefaultAsync(cancellationToken) ?? throw new ResourceNotFoundException("Document version not found.");
        if (version.ArchivedAtUtc.HasValue)
            throw new ConflictException("Archived matters cannot be changed.");
        if (version.Classification != DocumentClassification.Standard && !await db.DocumentSubmissions.AnyAsync(submission =>
                submission.DocumentVersionId == versionId && submission.Decision == SubmissionDecision.Accepted &&
                submission.ExternalRequest.UploadStatus == DocumentRequestStatus.Accepted &&
                submission.ExternalRequest.MatterId == matterId, cancellationToken))
            throw new ConflictException("Only the accepted client submission version can be signed.");
    }

    public async Task<DocumentVersionDto> UploadAsync(int matterId, int? documentId, DocumentRequest? request,
        Stream content, string fileName, int uploaderId, CancellationToken cancellationToken)
    {
        var matter = await db.Matters.FindAsync([matterId], cancellationToken)
            ?? throw new ResourceNotFoundException("Matter not found.");
        MatterRules.EnsureWritable(matter);
        if (!await db.Users.AnyAsync(user => user.Id == uploaderId, cancellationToken))
            throw new UnauthorizedAccessException();

        MatterDocument document;
        var number = 1;
        if (documentId.HasValue)
        {
            document = await db.MatterDocuments.SingleOrDefaultAsync(document =>
                document.Id == documentId && document.MatterId == matterId, cancellationToken)
                ?? throw new ResourceNotFoundException("Document not found.");
            if (document.Classification != DocumentClassification.Standard)
                throw new ConflictException("Client-request documents receive new versions through their request.");
            number = await db.DocumentVersions.Where(version => version.DocumentId == documentId)
                .MaxAsync(version => version.VersionNumber, cancellationToken) + 1;
        }
        else
        {
            ArgumentNullException.ThrowIfNull(request);
            document = new MatterDocument
            {
                Matter = matter, Title = request.Title.Trim(),
                Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim(),
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            db.MatterDocuments.Add(document);
        }

        var displayName = Path.GetFileName(fileName.Replace('\\', '/'));
        displayName = string.Concat(displayName.Where(character => !char.IsControl(character))).Trim();
        if (displayName.Length is 0 or > 255)
            throw new InvalidDocumentException("The PDF filename must contain between 1 and 255 characters.");

        var stored = await storage.SaveAsync(content, cancellationToken);
        try
        {
            var version = new DocumentVersion
            {
                Document = document, VersionNumber = number, OriginalFileName = displayName,
                ByteLength = stored.ByteLength, Sha256 = stored.Sha256, StorageKey = stored.Key,
                UploaderId = uploaderId, UploadedAtUtc = DateTimeOffset.UtcNow
            };
            db.DocumentVersions.Add(version);
            document.UpdatedAtUtc = version.UploadedAtUtc;
            activities.Record(matter, documentId.HasValue ? MatterActivityType.DocumentVersionUploaded : MatterActivityType.DocumentUploaded,
                documentId.HasValue ? $"Document #{documentId} version {number} uploaded." : $"Document uploaded: {document.Title} (version 1).");
            await SaveAsync(matter, cancellationToken);
            return new DocumentVersionDto(version.Id, version.DocumentId, version.VersionNumber, version.OriginalFileName,
                version.ContentType, version.ByteLength, version.Sha256, version.Source, version.UploaderId, version.UploadedAtUtc);
        }
        catch
        {
            storage.Delete(stored.Key);
            throw;
        }
    }

    private async Task SaveAsync(Matter matter, CancellationToken cancellationToken)
    {
        matter.UpdatedAtUtc = DateTimeOffset.UtcNow;
        // Updating the matter also detects concurrent archive/upload requests through its xmin token.
        db.Entry(matter).Property(item => item.UpdatedAtUtc).IsModified = true;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The matter changed during this request. Reload it and try again.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_DocumentVersions_DocumentId_VersionNumber" })
        {
            throw new ConflictException("Another version was uploaded concurrently. Reload the history and try again.");
        }
    }
}
