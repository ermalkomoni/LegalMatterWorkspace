using Amanet.Application.DTOs.MatterTasks;

namespace Amanet.Application.Services;

public interface IMatterTaskService
{
    Task<IReadOnlyList<MatterTaskDto>> ListAsync(int matterId, CancellationToken cancellationToken);
    Task<MatterTaskDto> GetAsync(int matterId, int id, CancellationToken cancellationToken);
    Task<MatterTaskDto> CreateAsync(int matterId, MatterTaskRequest request, CancellationToken cancellationToken);
    Task<MatterTaskDto> UpdateAsync(int matterId, int id, MatterTaskRequest request, CancellationToken cancellationToken);
    Task<MatterTaskDto> CompleteAsync(int matterId, int id, CancellationToken cancellationToken);
    Task<MatterTaskDto> ReopenAsync(int matterId, int id, CancellationToken cancellationToken);
    Task DeleteAsync(int matterId, int id, CancellationToken cancellationToken);
}
