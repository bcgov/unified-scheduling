namespace Unified.Training.Models;

public sealed record TrainingRequest
{
    public required string Code { get; init; }

    public required string Description { get; init; }

    public bool Mandatory { get; init; }

    public int? ValidityDays { get; init; }

    public int? AdvanceNoticeDays { get; init; }

    public bool Rotating { get; init; }

    public int? TrainingCategoryId { get; init; }

    public int Order { get; init; }
}
