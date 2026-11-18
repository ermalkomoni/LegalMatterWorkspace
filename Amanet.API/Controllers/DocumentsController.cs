using Amanet.Application.DTOs;
using Amanet.Application.DTOs.Documents;
using Amanet.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Amanet.API.Controllers;

[Route("api/v{version:apiVersion}/matters/{matterId:int}/documents")]
public sealed class DocumentsController(IDocumentService documents) : BaseController
{
	[HttpGet("/api/v{version:apiVersion}/documents")]
	public Task<PagedResult<DocumentDto>> List([FromQuery] DocumentListQuery query, CancellationToken cancellationToken)
	{
		return documents.ListAsync(query, cancellationToken);
	}

	[HttpGet("{documentId:int}")]
    public Task<DocumentDto> Get(int matterId, int documentId, CancellationToken cancellationToken)
    {
        return documents.GetAsync(matterId, documentId, cancellationToken);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(21 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 21 * 1024 * 1024)]
    public async Task<ActionResult<DocumentVersionDto>> Upload(int matterId, [FromForm] DocumentRequest request,
        IFormFile file, CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirst("sub")?.Value, out var uploaderId))
            return Unauthorized();

        await using var content = file.OpenReadStream();
        var version = await documents.UploadAsync(matterId, null, request, content, file.FileName, uploaderId, cancellationToken);
        return CreatedAtAction(nameof(Get), new { matterId, documentId = version.DocumentId, version = "1" }, version);
    }

    [HttpPut("{documentId:int}")]
    public Task<DocumentDto> Update(int matterId, int documentId, DocumentRequest request, CancellationToken cancellationToken)
    {
        return documents.UpdateAsync(matterId, documentId, request, cancellationToken);
    }

    [HttpGet("{documentId:int}/versions")]
    public Task<IReadOnlyList<DocumentVersionDto>> Versions(int matterId, int documentId, CancellationToken cancellationToken)
    {
        return documents.VersionsAsync(matterId, documentId, cancellationToken);
    }

    [HttpPost("{documentId:int}/versions")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(21 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 21 * 1024 * 1024)]
    public async Task<ActionResult<DocumentVersionDto>> UploadVersion(int matterId, int documentId,
        IFormFile file, CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirst("sub")?.Value, out var uploaderId))
            return Unauthorized();

        await using var content = file.OpenReadStream();
        var version = await documents.UploadAsync(matterId, documentId, null, content, file.FileName, uploaderId, cancellationToken);
        return CreatedAtAction(nameof(Versions), new { matterId, documentId, version = "1" }, version);
    }

    [HttpGet("{documentId:int}/versions/{versionNumber:int}/file")]
    public async Task<FileStreamResult> GetFile(int matterId, int documentId, int versionNumber,
        CancellationToken cancellationToken, [FromQuery] bool download = false)
    {
        var file = await documents.OpenFileAsync(matterId, documentId, versionNumber, cancellationToken);
        var disposition = new ContentDispositionHeaderValue(download ? "attachment" : "inline");
        disposition.SetHttpFileName(file.FileName);
        Response.Headers.ContentDisposition = disposition.ToString();
        Response.Headers.CacheControl = "no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return new FileStreamResult(file.Content, "application/pdf")
        {
            EnableRangeProcessing = true,
            LastModified = file.UploadedAtUtc,
            EntityTag = new EntityTagHeaderValue("\"" + file.Sha256 + "\"")
        };
    }
}
