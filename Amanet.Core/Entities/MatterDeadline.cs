using Amanet.Core.Enums;

namespace Amanet.Core.Entities;

public sealed class MatterDeadline
{
    public int Id { get; set; }
    public int MatterId { get; set; }
    public Matter Matter { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public WorkItemStatus Status { get; set; } = WorkItemStatus.Pending;
}
