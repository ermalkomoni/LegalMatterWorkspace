namespace Amanet.Application.DTOs.MatterNotes;

public sealed record MatterNoteDto(int Id, int MatterId,
    string Text, int AuthorId, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
