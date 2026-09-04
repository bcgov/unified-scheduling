using Unified.Common.Calendar.Scheduling.Models;

namespace Unified.Scheduling.Models;

public sealed record ShiftSeriesRequest : CalendarEventSeriesRequest
{
    public int? LunchAvailableMinutes { get; set; }

    public int WorkedLunchMinutes { get; set; } = 0;

    public IReadOnlyCollection<AssignmentSeriesLinkRequest> AssignmentSeriesLinks { get; init; } = [];
}
