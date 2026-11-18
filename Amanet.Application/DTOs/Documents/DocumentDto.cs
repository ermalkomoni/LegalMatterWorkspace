namespace Amanet.Application.DTOs.Documents;

public sealed record DocumentDto(int Id, int MatterId, string MatterReference, string Title, string? Category,
    int LatestVersionNumber, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);

public sealed record DocumentVersionDto(int Id, int DocumentId, int VersionNumber, string OriginalFileName,
    string ContentType, long ByteLength, string Sha256, string Source, int UploaderId, DateTimeOffset UploadedAtUtc);

// Controllers stream this service result; it is never serialized.
public sealed record DocumentFile(Stream Content, string FileName, string Sha256, DateTimeOffset UploadedAtUtc);
