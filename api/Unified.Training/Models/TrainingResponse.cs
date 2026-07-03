namespace Unified.Training.Models;

public sealed record TrainingResponse
{
    public required int Id { get; init; }

    public required string Code { get; init; }

    public required string Description { get; init; }

    public bool Mandatory { get; init; }

    public int? ValidityDays { get; init; }

    public int? AdvanceNoticeDays { get; init; }

    public bool Rotating { get; init; }

    public int? TrainingCategoryId { get; init; }

    public string? TrainingCategoryName { get; init; }

    public int Order { get; init; }

    public DateTimeOffset CreatedOn { get; init; }

    public DateTimeOffset? UpdatedOn { get; init; }
}
