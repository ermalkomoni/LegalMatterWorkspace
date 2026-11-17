namespace Amanet.Core.Entities;

public sealed class MatterNote
{
    public int Id { get; set; }
    public int MatterId { get; set; }
    public Matter Matter { get; set; } = null!;
    public string Text { get; set; } = string.Empty;
    public int AuthorId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
