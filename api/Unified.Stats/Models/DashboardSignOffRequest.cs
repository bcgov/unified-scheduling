namespace Unified.Stats.Models;

public sealed record DashboardSignOffRequest
{
    /// <summary>
    /// The IDs of the specific stat record entries to sign off.
    /// </summary>
    public required IReadOnlyList<int> EntryIds { get; init; }
}
