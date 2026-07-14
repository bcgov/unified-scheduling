using Unified.Core.Models;

namespace Unified.Training.Models;

public sealed record TrainingLookupResponse : LookupCodeEntityResponse
{
    public bool Mandatory { get; init; }

    public int? ValidityDays { get; init; }

    public int? AdvanceNoticeDays { get; init; }

    public bool Rotating { get; init; }

    public int? TrainingCategoryId { get; init; }

    public string? TrainingCategoryName { get; init; }

    public int Order { get; init; }
}
