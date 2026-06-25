namespace Unified.Stats.Models;

public sealed record DashboardSignOffResponse
{
    /// <summary>Number of entries marked as Signed Off.</summary>
    public int SignedOffCount { get; init; }
}
