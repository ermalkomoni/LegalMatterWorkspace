using System.Linq.Expressions;
using Amanet.Application.DTOs;
using Amanet.Application.DTOs.Clients;
using Amanet.Application.Exceptions;
using Amanet.Application.Services;
using Amanet.Core.Entities;
using Amanet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Amanet.Infrastructure.Services;

public sealed class ClientService(ApplicationDbContext db) : IClientService
{
    private static readonly Expression<Func<Client, ClientDto>> Projection = client =>
        new ClientDto(client.Id, client.Name, client.Email, client.Phone, client.PostalAddress);

    public async Task<PagedResult<ClientDto>> ListAsync(ListQuery query, CancellationToken cancellationToken)
    {
        var clients = db.Clients.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            clients = clients.Where(client => client.Name.ToLower().Contains(term) ||
                client.Email.ToLower().Contains(term) ||
                (client.Phone != null && client.Phone.ToLower().Contains(term)) ||
                (client.PostalAddress != null && client.PostalAddress.ToLower().Contains(term)));
        }

        var totalCount = await clients.CountAsync(cancellationToken);
        var offset = ((long)query.Page - 1) * query.PageSize;
        var items = offset > int.MaxValue ? [] : await clients.OrderBy(client => client.Name.ToLower())
            .ThenBy(client => client.Id).Skip((int)offset).Take(query.PageSize)
            .Select(Projection).ToListAsync(cancellationToken);
        return new PagedResult<ClientDto>(items, totalCount, query.Page, query.PageSize);
    }

    public async Task<ClientDto> GetAsync(int id, CancellationToken cancellationToken) =>
        await db.Clients.AsNoTracking().Where(client => client.Id == id).Select(Projection)
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new ResourceNotFoundException("Client not found.");

    public async Task<ClientDto> CreateAsync(ClientRequest request, CancellationToken cancellationToken)
    {
        var client = new Client();
        Apply(client, request);
        db.Clients.Add(client);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(client.Id, cancellationToken);
    }

    public async Task<ClientDto> UpdateAsync(int id, ClientRequest request, CancellationToken cancellationToken)
    {
        var client = await db.Clients.FindAsync([id], cancellationToken)
            ?? throw new ResourceNotFoundException("Client not found.");
        Apply(client, request);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(client.Id, cancellationToken);
    }

    private static void Apply(Client client, ClientRequest request)
    {
        client.Name = request.Name.Trim();
        client.Email = request.Email.Trim();
        client.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        client.PostalAddress = string.IsNullOrWhiteSpace(request.PostalAddress) ? null : request.PostalAddress.Trim();
    }
}
