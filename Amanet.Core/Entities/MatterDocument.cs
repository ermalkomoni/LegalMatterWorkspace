namespace Amanet.Core.Entities;

public enum DocumentClassification { Standard, ReviewPending, Accepted }

public sealed class MatterDocument
{
    public int Id { get; set; }
    public int MatterId { get; set; }
    public Matter Matter { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string? Category { get; set; }
    public DocumentClassification Classification { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public ICollection<DocumentVersion> Versions { get; set; } = [];
}

public sealed class DocumentVersion
{
    public int Id { get; init; }
    public int DocumentId { get; init; }
    public MatterDocument Document { get; init; } = null!;
    public int VersionNumber { get; init; }
    public string OriginalFileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/pdf";
    public long ByteLength { get; init; }
    public string Sha256 { get; init; } = string.Empty;
    public string StorageKey { get; init; } = string.Empty;
    public string Source { get; init; } = "AdminUpload";
    public int? UploaderId { get; init; }
    public DateTimeOffset UploadedAtUtc { get; init; }
}
