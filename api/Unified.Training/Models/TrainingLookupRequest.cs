using Unified.Core.Models;

namespace Unified.Training.Models;

public sealed record TrainingLookupRequest : LookupCodeRequest
{
    public string? Description { get; init; }

    public bool Mandatory { get; init; }

    public int? ValidityDays { get; init; }

    public int? AdvanceNoticeDays { get; init; }

    public bool Rotating { get; init; }

    public int? TrainingCategoryId { get; init; }

    public int Order { get; init; }
}
