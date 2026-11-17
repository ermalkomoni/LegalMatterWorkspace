using Amanet.Core.Enums;

namespace Amanet.Application.DTOs.MatterTasks;

public sealed record MatterTaskDto(int Id, int MatterId,
    string Title, string? Description, DateOnly? DueDate, WorkItemStatus Status, bool IsOverdue);
