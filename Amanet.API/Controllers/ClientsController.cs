using Amanet.Application.DTOs;
using Amanet.Application.DTOs.Clients;
using Amanet.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Amanet.API.Controllers;

[Route("api/v{version:apiVersion}/clients")]
public sealed class ClientsController(IClientService clients) : BaseController
{
    [HttpGet]
    public Task<PagedResult<ClientDto>> List([FromQuery] ListQuery query, CancellationToken cancellationToken)
    {
        return clients.ListAsync(query, cancellationToken);
    }

    [HttpGet("{id:int}")]
    public Task<ClientDto> Get(int id, CancellationToken cancellationToken)
    {
        return clients.GetAsync(id, cancellationToken);
    }

    [HttpPost]
    public async Task<ActionResult<ClientDto>> Create(ClientRequest request, CancellationToken cancellationToken)
    {
        var client = await clients.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = client.Id, version = "1" }, client);
    }

    [HttpPut("{id:int}")]
    public Task<ClientDto> Update(int id, ClientRequest request, CancellationToken cancellationToken)
    {
        return clients.UpdateAsync(id, request, cancellationToken);
    }
}
