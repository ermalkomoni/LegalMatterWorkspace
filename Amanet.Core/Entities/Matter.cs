using Amanet.Core.Enums;

namespace Amanet.Core.Entities;

public sealed class Matter
{
    public int Id { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int PrimaryClientId { get; set; }
    public Client PrimaryClient { get; set; } = null!;
    public MatterStatus Status { get; set; } = MatterStatus.Open;
    public MatterPriority Priority { get; set; } = MatterPriority.Normal;
    public DateOnly OpenedDate { get; set; }
    public DateOnly? TargetCloseDate { get; set; }
    public DateTimeOffset? ArchivedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public uint Version { get; set; }
}
