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

    /// <summary>
    /// When <c>true</c>, any existing active training record for the same
    /// UserId + TrainingId is superseded by this one while preserving the
    /// older record as history.
    /// </summary>
    public bool OverrideConflicts { get; init; }

    /// <summary>
    /// When <c>true</c>, the record is created even when an active record
    /// already exists for the same UserId + TrainingId (duplicate allowed).
    /// </summary>
    public bool AllowConflictingEvents { get; init; }
}
