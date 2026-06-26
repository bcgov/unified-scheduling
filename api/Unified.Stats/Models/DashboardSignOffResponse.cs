namespace Unified.Stats.Models;

public sealed record DashboardSignOffResponse
{
    /// <summary>Number of entries marked as Signed Off.</summary>
    public int SignedOffCount { get; init; }

    /// <summary>IDs of the entries that were actually signed off (may be fewer than requested if some were already signed off or out of scope).</summary>
    public IReadOnlyList<int> SignedOffIds { get; init; } = [];
}
