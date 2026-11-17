using Amanet.Application.DTOs;
using Amanet.Application.DTOs.Matters;

namespace Amanet.Application.Services;

public interface IMatterService
{
    Task<PagedResult<MatterDto>> ListAsync(MatterListQuery query, CancellationToken cancellationToken);
    Task<MatterDto> GetAsync(int id, CancellationToken cancellationToken);
    Task<MatterDto> CreateAsync(MatterRequest request, CancellationToken cancellationToken);
    Task<MatterDto> UpdateAsync(int id, MatterRequest request, CancellationToken cancellationToken);
    Task<MatterDto> ArchiveAsync(int id, CancellationToken cancellationToken);
    Task<MatterDto> RestoreAsync(int id, CancellationToken cancellationToken);
    Task<PagedResult<MatterActivityDto>> GetActivitiesAsync(int id, ListQuery query, CancellationToken cancellationToken);
}
