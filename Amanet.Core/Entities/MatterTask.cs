using Amanet.Core.Enums;

namespace Amanet.Core.Entities;

public sealed class MatterTask
{
    public int Id { get; set; }
    public int MatterId { get; set; }
    public Matter Matter { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly? DueDate { get; set; }
    public WorkItemStatus Status { get; set; } = WorkItemStatus.Pending;
}
