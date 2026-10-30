using Unified.Common.Calendar.Scheduling.Models;

namespace Unified.Leave.Models.Scheduling;

// Calendar contracts stay unchanged: only Leave translates its optional date/time input.
public sealed record NormalizedLeaveEntryRequest : CalendarEventEntryRequest
{
    public int? LeaveSeriesId { get; init; }
    public int LeaveTypeId { get; init; }
}

public sealed record NormalizedLeaveSeriesRequest : CalendarEventSeriesRequest
{
    public int LeaveTypeId { get; init; }
}
