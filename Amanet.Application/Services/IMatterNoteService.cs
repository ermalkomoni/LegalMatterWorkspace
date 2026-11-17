using Amanet.Application.DTOs.MatterNotes;

namespace Amanet.Application.Services;

public interface IMatterNoteService
{
    Task<IReadOnlyList<MatterNoteDto>> ListAsync(int matterId, CancellationToken cancellationToken);
    Task<MatterNoteDto> GetAsync(int matterId, int id, CancellationToken cancellationToken);
    Task<MatterNoteDto> CreateAsync(int matterId, MatterNoteRequest request, int authorId, CancellationToken cancellationToken);
    Task<MatterNoteDto> UpdateAsync(int matterId, int id, MatterNoteRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(int matterId, int id, CancellationToken cancellationToken);
}
