using Amanet.Application.DTOs.MatterTasks;
using Amanet.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Amanet.API.Controllers;

[Route("api/v{version:apiVersion}/matters/{matterId:int}/tasks")]
public sealed class MatterTasksController(IMatterTaskService tasks) : BaseController
{
	[HttpGet]
	public Task<IReadOnlyList<MatterTaskDto>> List(int matterId, CancellationToken cancellationToken)
	{
		return tasks.ListAsync(matterId, cancellationToken);
	}

	[HttpGet("{id:int}")]
	public Task<MatterTaskDto> Get(int matterId, int id, CancellationToken cancellationToken)
	{
		return tasks.GetAsync(matterId, id, cancellationToken);
	}

	[HttpPost]
    public async Task<ActionResult<MatterTaskDto>> Create(int matterId, MatterTaskRequest request, CancellationToken cancellationToken)
    {
        var item = await tasks.CreateAsync(matterId, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { matterId, id = item.Id, version = "1" }, item);
    }

	[HttpPut("{id:int}")]
	public Task<MatterTaskDto> Update(int matterId, int id, MatterTaskRequest request, CancellationToken cancellationToken)
	{
		return tasks.UpdateAsync(matterId, id, request, cancellationToken);
	}

	[HttpPost("{id:int}/complete")]
	public Task<MatterTaskDto> Complete(int matterId, int id, CancellationToken cancellationToken)
	{
		return tasks.CompleteAsync(matterId, id, cancellationToken);
	}

	[HttpPost("{id:int}/reopen")]
	public Task<MatterTaskDto> Reopen(int matterId, int id, CancellationToken cancellationToken)
	{
		return tasks.ReopenAsync(matterId, id, cancellationToken);
	}

	[HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int matterId, int id, CancellationToken cancellationToken)
    {
        await tasks.DeleteAsync(matterId, id, cancellationToken);
        return NoContent();
    }
}
