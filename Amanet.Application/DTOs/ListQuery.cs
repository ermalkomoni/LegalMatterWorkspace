using System.ComponentModel.DataAnnotations;

namespace Amanet.Application.DTOs;

public sealed class NotificationQuery : ListQuery
{
    public bool? IsActionable { get; init; }
}

public class ListQuery
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;

    [StringLength(200)]
    public string? Search { get; init; }
}
