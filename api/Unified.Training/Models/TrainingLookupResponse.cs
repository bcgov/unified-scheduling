using Unified.Core.Models;

namespace Unified.Training.Models;

public sealed record TrainingProfileTypeSummary
{
    public int Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;
}

public record TrainingLookupResponse : LookupCodeEntityResponse
{
    public bool Mandatory { get; init; }

    public int? ValidityDays { get; init; }

    public int? AdvanceNoticeDays { get; init; }

    public bool Rotating { get; init; }

    public int? TrainingCategoryId { get; init; }

    public string? TrainingCategoryName { get; init; }

    public int Order { get; init; }

    public IReadOnlyCollection<TrainingProfileTypeSummary> MandatoryTrainingProfiles { get; init; } = [];
}
