using Amanet.Core.Enums;

namespace Amanet.Core.Entities;

public sealed class MatterActivity
{
    public int Id { get; set; }
    public int MatterId { get; set; }
    public Matter Matter { get; set; } = null!;
    public MatterActivityType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; set; }
}
