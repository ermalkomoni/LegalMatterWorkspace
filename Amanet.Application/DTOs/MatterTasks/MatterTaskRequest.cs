using System.ComponentModel.DataAnnotations;

namespace Amanet.Application.DTOs.MatterTasks;

public sealed class MatterTaskRequest
{
    [Required, StringLength(200)]
    public string Title { get; init; } = string.Empty;

    [StringLength(5000)]
    public string? Description { get; init; }

    public DateOnly? DueDate { get; init; }
}
