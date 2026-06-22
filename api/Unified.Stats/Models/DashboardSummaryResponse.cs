namespace Unified.Stats.Models;

public sealed record DashboardSummaryResponse
{
    public decimal RegularHours { get; init; }
    public decimal OvertimeHours { get; init; }
    public int SubmittedCount { get; init; }
    public int TotalEntries { get; init; }
}
