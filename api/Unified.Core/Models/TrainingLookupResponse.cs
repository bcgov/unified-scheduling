namespace Unified.Core.Models;

public sealed record TrainingLookupResponse : LookupCodeEntityResponse
{
    public DateTimeOffset EffectiveDate { get; init; }

    public DateTimeOffset? ExpiryDate { get; init; }

    public bool Mandatory { get; init; }

    public int? ValidityDays { get; init; }

    public int? AdvanceNoticeDays { get; init; }

    public bool Rotating { get; init; }

    public int? TrainingCategoryId { get; init; }

    public string? TrainingCategoryName { get; init; }

    public int Order { get; init; }
}