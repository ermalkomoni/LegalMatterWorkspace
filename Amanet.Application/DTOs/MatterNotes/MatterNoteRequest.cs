using System.ComponentModel.DataAnnotations;

namespace Amanet.Application.DTOs.MatterNotes;

public sealed class MatterNoteRequest
{
    [Required, StringLength(10000)]
    public string Text { get; init; } = string.Empty;
}
