namespace Unified.Leave.Models.Scheduling;

/// <summary>
/// Leave entry input, normalized by Leave before entering the shared Calendar pipeline.
/// </summary>
public sealed record SchedulingLeaveEntryRequest : SchedulingLeaveRequest
{
    public int? LeaveSeriesId { get; init; }
    public DateTimeOffset? SeriesStartAtUtc { get; init; }
    public DateTimeOffset? SeriesEndAtUtc { get; init; }
}
