using Amanet.Application.DTOs.MatterNotes;
using Amanet.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amanet.API.Controllers;

[Route("api/v{version:apiVersion}/matters/{matterId:int}/notes")]
[Authorize(Roles = AdminAccess.Role)]
public sealed class MatterNotesController(IMatterNoteService notes) : BaseController
{
	[HttpGet]
	public Task<IReadOnlyList<MatterNoteDto>> List(int matterId, CancellationToken cancellationToken)
	{
		return notes.ListAsync(matterId, cancellationToken);
	}

	[HttpGet("{id:int}")]
	public Task<MatterNoteDto> Get(int matterId, int id, CancellationToken cancellationToken)
	{
		return notes.GetAsync(matterId, id, cancellationToken);
	}

	[HttpPost]
    public async Task<ActionResult<MatterNoteDto>> Create(int matterId, MatterNoteRequest request, CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirst("sub")?.Value, out var authorId))
            return Unauthorized();
        var item = await notes.CreateAsync(matterId, request, authorId, cancellationToken);
        return CreatedAtAction(nameof(Get), new { matterId, id = item.Id, version = "1" }, item);
    }

	[HttpPut("{id:int}")]
	public Task<MatterNoteDto> Update(int matterId, int id, MatterNoteRequest request, CancellationToken cancellationToken)
	{
		return notes.UpdateAsync(matterId, id, request, cancellationToken);
	}

	[HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int matterId, int id, CancellationToken cancellationToken)
    {
        await notes.DeleteAsync(matterId, id, cancellationToken);
        return NoContent();
    }
}
