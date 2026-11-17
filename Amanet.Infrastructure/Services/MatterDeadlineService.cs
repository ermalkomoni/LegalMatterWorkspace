using Amanet.Application.DTOs.MatterDeadlines;
using Amanet.Application.Exceptions;
using Amanet.Application.Services;
using Amanet.Core.Entities;
using Amanet.Core.Enums;
using Amanet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Amanet.Infrastructure.Services;

public sealed class MatterDeadlineService(ApplicationDbContext db, IMatterActivityService activities) : IMatterDeadlineService
{
    public async Task<IReadOnlyList<MatterDeadlineDto>> ListAsync(int matterId, CancellationToken cancellationToken)
    {
        await FindMatterAsync(matterId, cancellationToken);
        return await Project(matterId).ToListAsync(cancellationToken);
    }

    public async Task<MatterDeadlineDto> GetAsync(int matterId, int id, CancellationToken cancellationToken) =>
        await Project(matterId, id).SingleOrDefaultAsync(cancellationToken)
        ?? throw new ResourceNotFoundException("Deadline not found.");

    public async Task<MatterDeadlineDto> CreateAsync(int matterId, MatterDeadlineRequest request, CancellationToken cancellationToken)
    {
        var matter = await FindMatterAsync(matterId, cancellationToken);
        MatterRules.EnsureWritable(matter);
        var item = new MatterDeadline { Matter = matter };
        Apply(item, request);
        db.MatterDeadlines.Add(item);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await SaveAsync(matter, cancellationToken);
        activities.Record(matter, MatterActivityType.DeadlineCreated, "Deadline #" + item.Id + " created.");
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(matterId, item.Id, cancellationToken);
    }

    public async Task<MatterDeadlineDto> UpdateAsync(int matterId, int id, MatterDeadlineRequest request, CancellationToken cancellationToken)
    {
        var matter = await FindMatterAsync(matterId, cancellationToken);
        MatterRules.EnsureWritable(matter);
        var item = await FindAsync(matterId, id, cancellationToken);
        Apply(item, request);
        db.ChangeTracker.DetectChanges();
        if (db.Entry(item).State == EntityState.Modified)
        {
            activities.Record(matter, MatterActivityType.DeadlineUpdated, "Deadline #" + id + " updated.");
            await SaveAsync(matter, cancellationToken);
        }
        return await GetAsync(matterId, id, cancellationToken);
    }

    public async Task<MatterDeadlineDto> CompleteAsync(int matterId, int id, CancellationToken cancellationToken)
    {
        var matter = await FindMatterAsync(matterId, cancellationToken);
        MatterRules.EnsureWritable(matter);
        var item = await FindAsync(matterId, id, cancellationToken);
        if (item.Status != WorkItemStatus.Completed)
        {
            item.Status = WorkItemStatus.Completed;
            activities.Record(matter, MatterActivityType.DeadlineCompleted, "Deadline #" + id + " completed.");
            await SaveAsync(matter, cancellationToken);
        }
        return await GetAsync(matterId, id, cancellationToken);
    }

    public async Task DeleteAsync(int matterId, int id, CancellationToken cancellationToken)
    {
        var matter = await FindMatterAsync(matterId, cancellationToken);
        MatterRules.EnsureWritable(matter);
        var item = await FindAsync(matterId, id, cancellationToken);
        db.MatterDeadlines.Remove(item);
        activities.Record(matter, MatterActivityType.DeadlineDeleted, "Deadline #" + id + " deleted.");
        await SaveAsync(matter, cancellationToken);
    }

    private IQueryable<MatterDeadlineDto> Project(int matterId, int? id = null)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return db.MatterDeadlines.AsNoTracking().Where(item => item.MatterId == matterId && (!id.HasValue || item.Id == id))
            .OrderByDescending(item => item.Id)
            .Select(item => new MatterDeadlineDto(item.Id, item.MatterId, item.Title, item.Date, item.Status,
                item.Status == WorkItemStatus.Pending && item.Date < today));
    }

    private async Task<MatterDeadline> FindAsync(int matterId, int id, CancellationToken cancellationToken) =>
        await db.MatterDeadlines.SingleOrDefaultAsync(item => item.MatterId == matterId && item.Id == id, cancellationToken)
        ?? throw new ResourceNotFoundException("Deadline not found.");

    private async Task<Matter> FindMatterAsync(int matterId, CancellationToken cancellationToken) =>
        await db.Matters.FindAsync([matterId], cancellationToken)
        ?? throw new ResourceNotFoundException("Matter not found.");

    private static void Apply(MatterDeadline item, MatterDeadlineRequest request)
    {
        item.Title = request.Title.Trim();
        item.Date = request.Date!.Value;
    }

    private async Task SaveAsync(Matter matter, CancellationToken cancellationToken)
    {
        matter.UpdatedAtUtc = DateTimeOffset.UtcNow;
        db.Entry(matter).Property(item => item.UpdatedAtUtc).IsModified = true;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The matter changed during this request. Reload it and try again.");
        }
    }
}
