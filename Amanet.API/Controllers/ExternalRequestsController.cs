using Amanet.Application.Services;
using Amanet.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;

namespace Amanet.API.Controllers;

[Route("api/v{version:apiVersion}/matters/{matterId:int}/external-requests")]
public sealed class ExternalRequestsController(IExternalRequestService requests) : BaseController
{
	[HttpGet]
	public Task<IReadOnlyList<ExternalRequestDto>> List(int matterId, CancellationToken cancellationToken)
	{
		return requests.ListAsync(matterId, cancellationToken);
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
}

[AllowAnonymous]
[EnableRateLimiting("ExternalLinks")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/v{version:apiVersion}/external")]
public sealed class PublicExternalRequestsController(IExternalRequestService requests) : BaseController
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
    public async Task<FileStreamResult> Pdf(Guid requestId, CancellationToken cancellationToken)
    {
        ProtectResponse();
        var file = await requests.OpenPdfAsync(requestId, LinkToken(), cancellationToken);
        var disposition = new ContentDispositionHeaderValue("inline");
        disposition.SetHttpFileName(file.FileName);
        Response.Headers.ContentDisposition = disposition.ToString();
        return new FileStreamResult(file.Content, "application/pdf");
    }

    private string? LinkToken() => Request.Headers["X-Link-Token"] is { Count: 1 } values ? values[0] : null;

    private void ProtectResponse()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers.XContentTypeOptions = "nosniff";
    }
}
