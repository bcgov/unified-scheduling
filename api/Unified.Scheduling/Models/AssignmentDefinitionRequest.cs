namespace Unified.Scheduling.Models;

public sealed record AssignmentDefinitionRequest
{
    public int LocationId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public int AssignmentCategoryTypeId { get; init; }

    public int AssignmentSubCategoryTypeId { get; init; }

    public string? Color { get; init; }

    public string? DefaultStartTime { get; init; }

    public string? DefaultEndTime { get; init; }

    public int DefaultCapacity { get; init; }

    public DateTimeOffset EffectiveDateUtc { get; init; }

    public DateTimeOffset? ExpiryDateUtc { get; init; }
}
