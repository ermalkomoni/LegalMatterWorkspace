using System.ComponentModel.DataAnnotations;
using Amanet.Core.Enums;

namespace Amanet.Application.DTOs.Matters;

public enum ArchiveState { Active, Archived, All }

public sealed class MatterListQuery : ListQuery
{
    [Range(1, int.MaxValue)]
    public int? ClientId { get; init; }

    [EnumDataType(typeof(MatterStatus))]
    public MatterStatus? Status { get; init; }

    [EnumDataType(typeof(MatterPriority))]
    public MatterPriority? Priority { get; init; }

    [EnumDataType(typeof(ArchiveState))]
    public ArchiveState ArchiveState { get; init; } = ArchiveState.Active;
}
