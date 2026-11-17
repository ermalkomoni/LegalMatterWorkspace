using System.ComponentModel.DataAnnotations;

namespace Amanet.Application.DTOs.MatterDeadlines;

public sealed class MatterDeadlineRequest
{
    [Required, StringLength(200)]
    public string Title { get; init; } = string.Empty;

    [Required]
    public DateOnly? Date { get; init; }
}
