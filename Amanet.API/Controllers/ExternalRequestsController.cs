using Amanet.Application.Services;
using Amanet.Application.DTOs;
using Amanet.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;

namespace Amanet.API.Controllers;

[Route("api/v{version:apiVersion}/matters/{matterId:int}/external-requests")]
public sealed class ExternalRequestsController(IExternalRequestService requests, IClientDocumentRequestService submissions) : BaseController
{
    [HttpGet("/api/v{version:apiVersion}/external-requests")]
    public Task<IReadOnlyList<ExternalRequestDto>> ListAll(CancellationToken cancellationToken,
        [FromQuery] int? matterId = null, [FromQuery] DocumentRequestStatus? status = null)
    {
        return requests.ListAsync(matterId, status, cancellationToken);
    }

	[HttpGet]
	public Task<IReadOnlyList<ExternalRequestDto>> List(int matterId, CancellationToken cancellationToken,
        [FromQuery] DocumentRequestStatus? status = null)
	{
		return requests.ListAsync(matterId, status, cancellationToken);
	}

	[HttpPost]
    public async Task<ActionResult<ExternalRequestDto>> Create(int matterId, CreateExternalRequest input,
        CancellationToken cancellationToken)
    {
        var request = await requests.CreateAsync(matterId, input, cancellationToken);
        return CreatedAtAction(nameof(Get), new { matterId, requestId = request.Id, version = "1" }, request);
    }

	[HttpGet("{requestId:guid}")]
	public Task<ExternalRequestDto> Get(int matterId, Guid requestId, CancellationToken cancellationToken)
	{
		return requests.GetAsync(matterId, requestId, cancellationToken);
	}

	[HttpPost("{requestId:guid}/resend")]
	public Task<ExternalRequestDto> Resend(int matterId, Guid requestId, ResendExternalRequest input,
		CancellationToken cancellationToken)
	{
		return requests.ResendAsync(matterId, requestId, input.Template, cancellationToken);
	}

	[HttpPost("{requestId:guid}/revoke")]
	public Task<ExternalRequestDto> Revoke(int matterId, Guid requestId, CancellationToken cancellationToken)
	{
		return requests.RevokeAsync(matterId, requestId, cancellationToken);
	}

    [HttpPost("{requestId:guid}/submissions/{versionId:int}/review")]
    public async Task<ActionResult<ExternalRequestDto>> Review(int matterId, Guid requestId, int versionId,
        ReviewSubmissionRequest input, CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirst("sub")?.Value, out var reviewerId))
            return Unauthorized();
        return await submissions.ReviewAsync(matterId, requestId, versionId, input, reviewerId, cancellationToken);
    }
}

[AllowAnonymous]
[EnableRateLimiting("ExternalLinks")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/v{version:apiVersion}/external")]
public sealed class PublicExternalRequestsController(IExternalRequestService requests, IClientDocumentRequestService submissions,
    ISigningService signing) : BaseController
{
    [HttpGet("{linkAction}/{requestId:guid}")]
    public async Task<ActionResult<PublicRequestContext>> Context(string linkAction, Guid requestId,
        CancellationToken cancellationToken)
    {
        ProtectResponse();
        var action = linkAction switch
        {
            "upload" => ExternalAction.Upload,
            "sign" => ExternalAction.Sign,
            _ => (ExternalAction?)null
        };
        if (!action.HasValue)
            return NotFound();
        return await requests.GetContextAsync(requestId, action.Value, LinkToken(), cancellationToken);
    }

    [HttpGet("sign/{requestId:guid}/file")]
    public async Task<FileStreamResult> Pdf(Guid requestId, CancellationToken cancellationToken, [FromQuery] bool download = false)
    {
        ProtectResponse();
        var file = await requests.OpenPdfAsync(requestId, LinkToken(), cancellationToken);
        var disposition = new ContentDispositionHeaderValue(download ? "attachment" : "inline");
        disposition.SetHttpFileName(file.FileName);
        Response.Headers.ContentDisposition = disposition.ToString();
        return new FileStreamResult(file.Content, "application/pdf");
    }

    [HttpPost("sign/{requestId:guid}/viewed")]
    public Task<PublicRequestContext> Viewed(Guid requestId, CancellationToken cancellationToken)
    {
        ProtectResponse();
        return signing.AdvanceAsync(requestId, LinkToken(), RequestProgress.Viewed, cancellationToken);
    }

    [HttpPost("sign/{requestId:guid}/started")]
    public Task<PublicRequestContext> Started(Guid requestId, CancellationToken cancellationToken)
    {
        ProtectResponse();
        return signing.AdvanceAsync(requestId, LinkToken(), RequestProgress.Started, cancellationToken);
    }

    [HttpPost("sign/{requestId:guid}")]
    public Task<SignatureDto> Sign(Guid requestId, SignRequest input, CancellationToken cancellationToken)
    {
        ProtectResponse();
        return signing.SignClientAsync(requestId, LinkToken(), input, cancellationToken);
    }

    [HttpPost("upload/{requestId:guid}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(21 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 21 * 1024 * 1024)]
    public async Task<PublicRequestContext> Upload(Guid requestId, IFormFile file, CancellationToken cancellationToken)
    {
        ProtectResponse();
        await using var content = file.OpenReadStream();
        return await submissions.UploadAsync(requestId, LinkToken(), content, file.FileName, cancellationToken);
    }

    private string? LinkToken()
    {
        return Request.Headers["X-Link-Token"] is { Count: 1 } values ? values[0] : null;
    }

    private void ProtectResponse()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers.XContentTypeOptions = "nosniff";
    }
}

[Route("api/v{version:apiVersion}/matters/{matterId:int}/documents/{documentId:int}")]
public sealed class AdminSigningController(ISigningService signing) : BaseController
{
    [HttpPost("sign")]
    public async Task<ActionResult<SignatureDto>> Sign(int matterId, int documentId, AdminSignRequest input,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirst("sub")?.Value, out var adminId))
            return Unauthorized();
        return await signing.SignAdminAsync(matterId, documentId, input, adminId, cancellationToken);
    }

    [HttpGet("signature")]
    public Task<SignatureDto> Get(int matterId, int documentId, CancellationToken cancellationToken)
    {
        return signing.GetAsync(matterId, documentId, cancellationToken);
    }
}

[Route("api/v{version:apiVersion}/notifications")]
public sealed class AdminNotificationsController(IAdminNotificationService notifications) : BaseController
{
    [HttpGet]
    public Task<PagedResult<AdminNotificationDto>> List([FromQuery] NotificationQuery query, CancellationToken cancellationToken)
    {
        return notifications.ListAsync(query, cancellationToken);
    }
}
