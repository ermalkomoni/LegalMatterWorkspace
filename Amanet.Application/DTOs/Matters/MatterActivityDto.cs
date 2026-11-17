using Amanet.Core.Enums;

namespace Amanet.Application.DTOs.Matters;

public sealed record MatterActivityDto(int Id, MatterActivityType Type, string Description,
    DateTimeOffset OccurredAtUtc);
