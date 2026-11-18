using Amanet.Application.DTOs;
using Amanet.Application.DTOs.Documents;

namespace Amanet.Application.Services;

public interface IDocumentService
{
    Task<PagedResult<DocumentDto>> ListAsync(DocumentListQuery query, CancellationToken cancellationToken);
    Task<DocumentDto> GetAsync(int matterId, int documentId, CancellationToken cancellationToken);
    Task<DocumentDto> UpdateAsync(int matterId, int documentId, DocumentRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<DocumentVersionDto>> VersionsAsync(int matterId, int documentId, CancellationToken cancellationToken);
    Task<DocumentFile> OpenFileAsync(int matterId, int documentId, int versionNumber, CancellationToken cancellationToken);
    Task EnsureSignableAsync(int matterId, int versionId, CancellationToken cancellationToken);
    Task<DocumentVersionDto> UploadAsync(int matterId, int? documentId, DocumentRequest? request,
        Stream content, string fileName, int uploaderId, CancellationToken cancellationToken);
}
