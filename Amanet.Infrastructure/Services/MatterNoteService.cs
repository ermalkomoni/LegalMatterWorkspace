using Amanet.Application.DTOs.MatterNotes;
using Amanet.Application.Exceptions;
using Amanet.Application.Services;
using Amanet.Core.Entities;
using Amanet.Core.Enums;
using Amanet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Amanet.Infrastructure.Services;

public sealed class MatterNoteService(ApplicationDbContext db, IMatterActivityService activities) : IMatterNoteService
{
    public async Task<IReadOnlyList<MatterNoteDto>> ListAsync(int matterId, CancellationToken cancellationToken)
    {
        await FindMatterAsync(matterId, cancellationToken);
        return await Project(matterId).ToListAsync(cancellationToken);
    }

    public async Task<MatterNoteDto> GetAsync(int matterId, int id, CancellationToken cancellationToken) =>
        await Project(matterId, id).SingleOrDefaultAsync(cancellationToken)
        ?? throw new ResourceNotFoundException("Note not found.");

    public async Task<MatterNoteDto> CreateAsync(int matterId, MatterNoteRequest request, int authorId, CancellationToken cancellationToken)
    {
        var matter = await FindMatterAsync(matterId, cancellationToken);
        MatterRules.EnsureWritable(matter);
        if (!await db.Users.AnyAsync(user => user.Id == authorId, cancellationToken))
            throw new UnauthorizedAccessException();
        var now = DateTimeOffset.UtcNow;
        var note = new MatterNote { Matter = matter, AuthorId = authorId, CreatedAtUtc = now, UpdatedAtUtc = now };
        Apply(note, request);
        db.MatterNotes.Add(note);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await SaveAsync(matter, cancellationToken);
        activities.Record(matter, MatterActivityType.NoteCreated, "Internal note #" + note.Id + " created.");
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(matterId, note.Id, cancellationToken);
    }

    public async Task<MatterNoteDto> UpdateAsync(int matterId, int id, MatterNoteRequest request, CancellationToken cancellationToken)
    {
        var matter = await FindMatterAsync(matterId, cancellationToken);
        MatterRules.EnsureWritable(matter);
        var note = await FindAsync(matterId, id, cancellationToken);
        Apply(note, request);
        db.ChangeTracker.DetectChanges();
        if (db.Entry(note).State == EntityState.Modified)
        {
            note.UpdatedAtUtc = DateTimeOffset.UtcNow;
            activities.Record(matter, MatterActivityType.NoteUpdated, "Internal note #" + id + " updated.");
            await SaveAsync(matter, cancellationToken);
        }
        return await GetAsync(matterId, id, cancellationToken);
    }

    public async Task DeleteAsync(int matterId, int id, CancellationToken cancellationToken)
    {
        var matter = await FindMatterAsync(matterId, cancellationToken);
        MatterRules.EnsureWritable(matter);
        var note = await FindAsync(matterId, id, cancellationToken);
        db.MatterNotes.Remove(note);
        activities.Record(matter, MatterActivityType.NoteDeleted, "Internal note #" + id + " deleted.");
        await SaveAsync(matter, cancellationToken);
    }

    private IQueryable<MatterNoteDto> Project(int matterId, int? id = null)
    {
        return db.MatterNotes.AsNoTracking().Where(item => item.MatterId == matterId && (!id.HasValue || item.Id == id))
            .OrderByDescending(item => item.Id)
            .Select(note => new MatterNoteDto(note.Id, note.MatterId, note.Text, note.AuthorId, note.CreatedAtUtc, note.UpdatedAtUtc));
    }

    private async Task<MatterNote> FindAsync(int matterId, int id, CancellationToken cancellationToken) =>
        await db.MatterNotes.SingleOrDefaultAsync(item => item.MatterId == matterId && item.Id == id, cancellationToken)
        ?? throw new ResourceNotFoundException("Note not found.");

    private async Task<Matter> FindMatterAsync(int matterId, CancellationToken cancellationToken) =>
        await db.Matters.FindAsync([matterId], cancellationToken)
        ?? throw new ResourceNotFoundException("Matter not found.");

    private static void Apply(MatterNote note, MatterNoteRequest request)
    {
        note.Text = request.Text.Trim();
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
