using Amanet.Application.DTOs;
using Amanet.Application.DTOs.Matters;
using Amanet.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Amanet.API.Controllers;

[Route("api/v{version:apiVersion}/matters")]
public sealed class MattersController(IMatterService matters) : BaseController
{
    [HttpGet]
    public Task<PagedResult<MatterDto>> List([FromQuery] MatterListQuery query, CancellationToken cancellationToken)
    {
        return matters.ListAsync(query, cancellationToken);
    }

    [HttpGet("{id:int}")]
    public Task<MatterDto> Get(int id, CancellationToken cancellationToken)
    {

        return matters.GetAsync(id, cancellationToken);
    }

    [HttpPost]
    public async Task<ActionResult<MatterDto>> Create(MatterRequest request, CancellationToken cancellationToken)
    {
        var matter = await matters.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = matter.Id, version = "1" }, matter);
    }

    [HttpPut("{id:int}")]
    public Task<MatterDto> Update(int id, MatterRequest request, CancellationToken cancellationToken)
    {
        return matters.UpdateAsync(id, request, cancellationToken);
    }

    [HttpPost("{id:int}/archive")]
    public Task<MatterDto> Archive(int id, CancellationToken cancellationToken)
    {
        return matters.ArchiveAsync(id, cancellationToken);
    }

    [HttpPost("{id:int}/restore")]
    public Task<MatterDto> Restore(int id, CancellationToken cancellationToken)
    {
        return matters.RestoreAsync(id, cancellationToken);
    }

    [HttpGet("{id:int}/activities")]
    public Task<PagedResult<MatterActivityDto>> Activities(int id, [FromQuery] ListQuery query,
        CancellationToken cancellationToken)
    {
        return matters.GetActivitiesAsync(id, query, cancellationToken);
    }
}