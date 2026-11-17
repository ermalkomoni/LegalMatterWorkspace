using Amanet.Core.Enums;

namespace Amanet.Application.DTOs.MatterDeadlines;

public sealed record MatterDeadlineDto(int Id, int MatterId,
    string Title, DateOnly Date, WorkItemStatus Status, bool IsOverdue);
