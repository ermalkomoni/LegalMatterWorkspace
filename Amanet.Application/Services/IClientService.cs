using Amanet.Application.DTOs;
using Amanet.Application.DTOs.Clients;

namespace Amanet.Application.Services;

public interface IClientService
{
    Task<PagedResult<ClientDto>> ListAsync(ListQuery query, CancellationToken cancellationToken);
    Task<ClientDto> GetAsync(int id, CancellationToken cancellationToken);
    Task<ClientDto> CreateAsync(ClientRequest request, CancellationToken cancellationToken);
    Task<ClientDto> UpdateAsync(int id, ClientRequest request, CancellationToken cancellationToken);
}
