namespace Unified.Leave.Models.Scheduling;

/// <summary>
/// Recurring leave input, normalized before Calendar validates and materializes the series.
/// </summary>
public sealed record SchedulingLeaveSeriesRequest : SchedulingLeaveRequest
{
    public string? RecurrenceRule { get; init; }
}
