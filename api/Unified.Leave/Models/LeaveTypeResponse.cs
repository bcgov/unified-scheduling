namespace Unified.Leave.Models;

public sealed record LeaveTypeResponse
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsPaid { get; init; }

    /// <summary>First day the leave type is available (inclusive), as a UTC-midnight business date.</summary>
    public DateTimeOffset EffectiveDateUtc { get; init; }

    /// <summary>Day the leave type stops being available (exclusive), as a UTC-midnight business date; null if it does not expire.</summary>
    public DateTimeOffset? ExpiryDateUtc { get; init; }
}
