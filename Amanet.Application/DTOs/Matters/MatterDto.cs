using Amanet.Core.Enums;

namespace Amanet.Application.DTOs.Matters;

public sealed record MatterDto(int Id, string ReferenceNumber, string Title, string Description,
    int PrimaryClientId, string PrimaryClientName, MatterStatus Status, MatterPriority Priority,
    DateOnly OpenedDate, DateOnly? TargetCloseDate, DateTimeOffset? ArchivedAtUtc,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
