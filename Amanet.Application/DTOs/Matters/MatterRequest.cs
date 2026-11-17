using System.ComponentModel.DataAnnotations;
using Amanet.Core.Enums;

namespace Amanet.Application.DTOs.Matters;

public sealed class MatterRequest : IValidatableObject
{
    [Required, StringLength(50)]
    public string ReferenceNumber { get; init; } = string.Empty;

    [Required, StringLength(200)]
    public string Title { get; init; } = string.Empty;

    [Required, StringLength(5000)]
    public string Description { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int PrimaryClientId { get; init; }

    [EnumDataType(typeof(MatterStatus))]
    public MatterStatus Status { get; init; } = MatterStatus.Open;

    [EnumDataType(typeof(MatterPriority))]
    public MatterPriority Priority { get; init; } = MatterPriority.Normal;

    [Required]
    public DateOnly? OpenedDate { get; init; }
    public DateOnly? TargetCloseDate { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (OpenedDate.HasValue && TargetCloseDate.HasValue && TargetCloseDate.Value < OpenedDate.Value)
        {
            yield return new ValidationResult("Target close date cannot precede the opened date.",
                [nameof(TargetCloseDate)]);
        }
    }
}
