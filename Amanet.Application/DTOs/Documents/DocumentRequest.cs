using System.ComponentModel.DataAnnotations;

namespace Amanet.Application.DTOs.Documents;

public sealed class DocumentRequest
{
    [Required, StringLength(200)]
    public string Title { get; init; } = string.Empty;

    [StringLength(100)]
    public string? Category { get; init; }
}

public sealed class DocumentListQuery : ListQuery
{
    [Range(1, int.MaxValue)]
    public int? MatterId { get; init; }
}
