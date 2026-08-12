namespace Unified.Training.Models;

public sealed record UserTrainingRequest
{
    public required Guid UserId { get; init; }

    public required int TrainingId { get; init; }

    public required DateTimeOffset AwardedOn { get; init; }
    public required DateTimeOffset EndingOn { get; init; }

    /// <summary>
    /// Explicit expiry date. When null, expiry is auto-calculated from the
    /// training type's <c>ValidityDays</c>.</summary>
    public DateTimeOffset? ExpiryDate { get; init; }

    public string? Notes { get; init; }
}
