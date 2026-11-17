using System.Linq.Expressions;
using Amanet.Application.DTOs;
using Amanet.Application.DTOs.Matters;
using Amanet.Application.Exceptions;
using Amanet.Application.Services;
using Amanet.Core.Entities;
using Amanet.Core.Enums;
using Amanet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Amanet.Infrastructure.Services;

public sealed class MatterService(ApplicationDbContext db, IMatterActivityService activities) : IMatterService
{
    private static readonly Expression<Func<Matter, MatterDto>> Projection = matter =>
        new MatterDto(matter.Id, matter.ReferenceNumber, matter.Title, matter.Description,
            matter.PrimaryClientId, matter.PrimaryClient.Name, matter.Status, matter.Priority,
            matter.OpenedDate, matter.TargetCloseDate, matter.ArchivedAtUtc, matter.CreatedAtUtc, matter.UpdatedAtUtc);

    public async Task<PagedResult<MatterDto>> ListAsync(MatterListQuery query, CancellationToken cancellationToken)
    {
        var matters = db.Matters.AsNoTracking();
        if (query.ArchiveState == ArchiveState.Active)
            matters = matters.Where(matter => matter.ArchivedAtUtc == null);
        else if (query.ArchiveState == ArchiveState.Archived)
            matters = matters.Where(matter => matter.ArchivedAtUtc != null);

        if (query.ClientId.HasValue)
            matters = matters.Where(matter => matter.PrimaryClientId == query.ClientId);
        if (query.Status.HasValue)
            matters = matters.Where(matter => matter.Status == query.Status);
        if (query.Priority.HasValue)
            matters = matters.Where(matter => matter.Priority == query.Priority);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            matters = matters.Where(matter => matter.ReferenceNumber.ToLower().Contains(term) ||
                matter.Title.ToLower().Contains(term) || matter.Description.ToLower().Contains(term) ||
                matter.PrimaryClient.Name.ToLower().Contains(term));
        }

        var totalCount = await matters.CountAsync(cancellationToken);
        var offset = ((long)query.Page - 1) * query.PageSize;
        var items = offset > int.MaxValue ? [] : await matters.OrderByDescending(matter => matter.OpenedDate)
            .ThenByDescending(matter => matter.Id).Skip((int)offset).Take(query.PageSize)
            .Select(Projection).ToListAsync(cancellationToken);
        return new PagedResult<MatterDto>(items, totalCount, query.Page, query.PageSize);
    }

    public async Task<MatterDto> GetAsync(int id, CancellationToken cancellationToken) =>
        await db.Matters.AsNoTracking().Where(matter => matter.Id == id).Select(Projection)
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new ResourceNotFoundException("Matter not found.");

    public async Task<MatterDto> CreateAsync(MatterRequest request, CancellationToken cancellationToken)
    {
        await EnsureClientExistsAsync(request.PrimaryClientId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var matter = new Matter { CreatedAtUtc = now, UpdatedAtUtc = now };
        Apply(matter, request);
        db.Matters.Add(matter);
        activities.Record(matter, MatterActivityType.Created, "Matter created.");
        await SaveAsync(cancellationToken);
        return await GetAsync(matter.Id, cancellationToken);
    }

    public async Task<MatterDto> UpdateAsync(int id, MatterRequest request, CancellationToken cancellationToken)
    {
        var matter = await FindAsync(id, cancellationToken);
        MatterRules.EnsureWritable(matter);
        await EnsureClientExistsAsync(request.PrimaryClientId, cancellationToken);
        Apply(matter, request);
        db.ChangeTracker.DetectChanges();
        var changes = db.Entry(matter).Properties.Where(property => property.IsModified)
            .Select(property => property.Metadata.Name).ToArray();
        if (changes.Length > 0)
        {
            matter.UpdatedAtUtc = DateTimeOffset.UtcNow;
            activities.Record(matter, MatterActivityType.Updated, "Updated: " + string.Join(", ", changes) + ".");
            await SaveAsync(cancellationToken);
        }
        return await GetAsync(id, cancellationToken);
    }

    public async Task<MatterDto> ArchiveAsync(int id, CancellationToken cancellationToken)
    {
        var matter = await FindAsync(id, cancellationToken);
        if (matter.ArchivedAtUtc is null)
        {
            matter.ArchivedAtUtc = DateTimeOffset.UtcNow;
            matter.UpdatedAtUtc = matter.ArchivedAtUtc.Value;
            activities.Record(matter, MatterActivityType.Archived, "Matter archived.");
            await SaveAsync(cancellationToken);
        }
        return await GetAsync(id, cancellationToken);
    }

    public async Task<MatterDto> RestoreAsync(int id, CancellationToken cancellationToken)
    {
        var matter = await FindAsync(id, cancellationToken);
        if (matter.ArchivedAtUtc.HasValue)
        {
            matter.ArchivedAtUtc = null;
            matter.UpdatedAtUtc = DateTimeOffset.UtcNow;
            activities.Record(matter, MatterActivityType.Restored, "Matter restored.");
            await SaveAsync(cancellationToken);
        }
        return await GetAsync(id, cancellationToken);
    }

    public async Task<PagedResult<MatterActivityDto>> GetActivitiesAsync(int id, ListQuery query,
        CancellationToken cancellationToken)
    {
        if (!await db.Matters.AnyAsync(matter => matter.Id == id, cancellationToken))
            throw new ResourceNotFoundException("Matter not found.");

        var records = db.MatterActivities.AsNoTracking().Where(activity => activity.MatterId == id);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            records = records.Where(activity => activity.Description.ToLower().Contains(term));
        }
        var totalCount = await records.CountAsync(cancellationToken);
        var offset = ((long)query.Page - 1) * query.PageSize;
        var items = offset > int.MaxValue ? [] : await records.OrderByDescending(activity => activity.OccurredAtUtc)
            .ThenByDescending(activity => activity.Id).Skip((int)offset).Take(query.PageSize)
            .Select(activity => new MatterActivityDto(activity.Id, activity.Type, activity.Description, activity.OccurredAtUtc))
            .ToListAsync(cancellationToken);
        return new PagedResult<MatterActivityDto>(items, totalCount, query.Page, query.PageSize);
    }

    private async Task<Matter> FindAsync(int id, CancellationToken cancellationToken) =>
        await db.Matters.FindAsync([id], cancellationToken)
        ?? throw new ResourceNotFoundException("Matter not found.");

    private async Task EnsureClientExistsAsync(int id, CancellationToken cancellationToken)
    {
        if (!await db.Clients.AnyAsync(client => client.Id == id, cancellationToken))
            throw new ResourceNotFoundException("Primary client not found.");
    }

    private static void Apply(Matter matter, MatterRequest request)
    {
        matter.ReferenceNumber = request.ReferenceNumber.Trim().ToUpperInvariant();
        matter.Title = request.Title.Trim();
        matter.Description = request.Description.Trim();
        matter.PrimaryClientId = request.PrimaryClientId;
        matter.Status = request.Status;
        matter.Priority = request.Priority;
        matter.OpenedDate = request.OpenedDate!.Value;
        matter.TargetCloseDate = request.TargetCloseDate;
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
               { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Matters_ReferenceNumber" })
        {
            throw new ConflictException("A matter with this reference number already exists.");
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The matter changed during this request. Reload it and try again.");
        }
    }
}
