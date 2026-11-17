using Amanet.Application.DTOs.MatterTasks;
using Amanet.Application.Exceptions;
using Amanet.Application.Services;
using Amanet.Core.Entities;
using Amanet.Core.Enums;
using Amanet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Amanet.Infrastructure.Services;

public sealed class MatterTaskService(ApplicationDbContext db, IMatterActivityService activities) : IMatterTaskService
{
    public async Task<IReadOnlyList<MatterTaskDto>> ListAsync(int matterId, CancellationToken cancellationToken)
    {
        await FindMatterAsync(matterId, cancellationToken);
        return await Project(matterId).ToListAsync(cancellationToken);
    }

    public async Task<MatterTaskDto> GetAsync(int matterId, int id, CancellationToken cancellationToken) =>
        await Project(matterId, id).SingleOrDefaultAsync(cancellationToken)
        ?? throw new ResourceNotFoundException("Task not found.");

    public async Task<MatterTaskDto> CreateAsync(int matterId, MatterTaskRequest request, CancellationToken cancellationToken)
    {
        var matter = await FindMatterAsync(matterId, cancellationToken);
        MatterRules.EnsureWritable(matter);
        var item = new MatterTask { Matter = matter };
        Apply(item, request);
        db.MatterTasks.Add(item);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await SaveAsync(matter, cancellationToken);
        activities.Record(matter, MatterActivityType.TaskCreated, "Task #" + item.Id + " created.");
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(matterId, item.Id, cancellationToken);
    }

    public async Task<MatterTaskDto> UpdateAsync(int matterId, int id, MatterTaskRequest request, CancellationToken cancellationToken)
    {
        var matter = await FindMatterAsync(matterId, cancellationToken);
        MatterRules.EnsureWritable(matter);
        var item = await FindAsync(matterId, id, cancellationToken);
        Apply(item, request);
        db.ChangeTracker.DetectChanges();
        if (db.Entry(item).State == EntityState.Modified)
        {
            activities.Record(matter, MatterActivityType.TaskUpdated, "Task #" + id + " updated.");
            await SaveAsync(matter, cancellationToken);
        }
        return await GetAsync(matterId, id, cancellationToken);
    }

    public Task<MatterTaskDto> CompleteAsync(int matterId, int id, CancellationToken cancellationToken) =>
        SetStatusAsync(matterId, id, WorkItemStatus.Completed, cancellationToken);

    public Task<MatterTaskDto> ReopenAsync(int matterId, int id, CancellationToken cancellationToken) =>
        SetStatusAsync(matterId, id, WorkItemStatus.Pending, cancellationToken);

    private async Task<MatterTaskDto> SetStatusAsync(int matterId, int id, WorkItemStatus status, CancellationToken cancellationToken)
    {
        var matter = await FindMatterAsync(matterId, cancellationToken);
        MatterRules.EnsureWritable(matter);
        var item = await FindAsync(matterId, id, cancellationToken);
        if (item.Status != status)
        {
            item.Status = status;
            activities.Record(matter, status == WorkItemStatus.Completed ? MatterActivityType.TaskCompleted : MatterActivityType.TaskReopened,
                "Task #" + id + (status == WorkItemStatus.Completed ? " completed." : " reopened."));
            await SaveAsync(matter, cancellationToken);
        }
        return await GetAsync(matterId, id, cancellationToken);
    }

    public async Task DeleteAsync(int matterId, int id, CancellationToken cancellationToken)
    {
        var matter = await FindMatterAsync(matterId, cancellationToken);
        MatterRules.EnsureWritable(matter);
        var item = await FindAsync(matterId, id, cancellationToken);
        db.MatterTasks.Remove(item);
        activities.Record(matter, MatterActivityType.TaskDeleted, "Task #" + id + " deleted.");
        await SaveAsync(matter, cancellationToken);
    }

    private IQueryable<MatterTaskDto> Project(int matterId, int? id = null)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return db.MatterTasks.AsNoTracking().Where(item => item.MatterId == matterId && (!id.HasValue || item.Id == id))
            .OrderByDescending(item => item.Id)
            .Select(item => new MatterTaskDto(item.Id, item.MatterId, item.Title, item.Description, item.DueDate, item.Status,
                item.Status == WorkItemStatus.Pending && item.DueDate.HasValue && item.DueDate.Value < today));
    }

    private async Task<MatterTask> FindAsync(int matterId, int id, CancellationToken cancellationToken) =>
        await db.MatterTasks.SingleOrDefaultAsync(item => item.MatterId == matterId && item.Id == id, cancellationToken)
        ?? throw new ResourceNotFoundException("Task not found.");

    private async Task<Matter> FindMatterAsync(int matterId, CancellationToken cancellationToken) =>
        await db.Matters.FindAsync([matterId], cancellationToken)
        ?? throw new ResourceNotFoundException("Matter not found.");

    private static void Apply(MatterTask item, MatterTaskRequest request)
    {
        item.Title = request.Title.Trim();
        item.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        item.DueDate = request.DueDate;
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
