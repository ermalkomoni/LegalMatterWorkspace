using Amanet.Application.DTOs.MatterDeadlines;

namespace Amanet.Application.Services;

public interface IMatterDeadlineService
{
    Task<IReadOnlyList<MatterDeadlineDto>> ListAsync(int matterId, CancellationToken cancellationToken);
    Task<MatterDeadlineDto> GetAsync(int matterId, int id, CancellationToken cancellationToken);
    Task<MatterDeadlineDto> CreateAsync(int matterId, MatterDeadlineRequest request, CancellationToken cancellationToken);
    Task<MatterDeadlineDto> UpdateAsync(int matterId, int id, MatterDeadlineRequest request, CancellationToken cancellationToken);
    Task<MatterDeadlineDto> CompleteAsync(int matterId, int id, CancellationToken cancellationToken);
    Task DeleteAsync(int matterId, int id, CancellationToken cancellationToken);
}
