namespace Unified.Leave.Models;

public sealed record LeaveTypeRequest
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsPaid { get; init; }

    /// <summary>
    /// First day the leave type is available (inclusive). Normalized to UTC midnight of the UTC date;
    /// defaults to today when omitted.
    /// </summary>
    public DateTimeOffset? EffectiveDateUtc { get; init; }

    /// <summary>
    /// Day the leave type stops being available (exclusive). Normalized to UTC midnight of the UTC date;
    /// null means it does not expire.
    /// </summary>
    public DateTimeOffset? ExpiryDateUtc { get; init; }
}
