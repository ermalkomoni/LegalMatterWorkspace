using Amanet.Application.DTOs.MatterDeadlines;
using Amanet.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Amanet.API.Controllers;

[Route("api/v{version:apiVersion}/matters/{matterId:int}/deadlines")]
public sealed class MatterDeadlinesController(IMatterDeadlineService deadlines) : BaseController
{
	[HttpGet]
	public Task<IReadOnlyList<MatterDeadlineDto>> List(int matterId, CancellationToken cancellationToken)
	{
		return deadlines.ListAsync(matterId, cancellationToken);
	}

	[HttpGet("{id:int}")]
	public Task<MatterDeadlineDto> Get(int matterId, int id, CancellationToken cancellationToken)
	{
		return deadlines.GetAsync(matterId, id, cancellationToken);
	}

	[HttpPost]
    public async Task<ActionResult<MatterDeadlineDto>> Create(int matterId, MatterDeadlineRequest request, CancellationToken cancellationToken)
    {
        var item = await deadlines.CreateAsync(matterId, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { matterId, id = item.Id, version = "1" }, item);
    }

	[HttpPut("{id:int}")]
	public Task<MatterDeadlineDto> Update(int matterId, int id, MatterDeadlineRequest request, CancellationToken cancellationToken)
	{
		return deadlines.UpdateAsync(matterId, id, request, cancellationToken);
	}

	[HttpPost("{id:int}/complete")]
	public Task<MatterDeadlineDto> Complete(int matterId, int id, CancellationToken cancellationToken)
	{
		return deadlines.CompleteAsync(matterId, id, cancellationToken);
	}

	[HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int matterId, int id, CancellationToken cancellationToken)
    {
        await deadlines.DeleteAsync(matterId, id, cancellationToken);
        return NoContent();
    }
}
