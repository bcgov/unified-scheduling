using Unified.Common.Calendar.Scheduling.Models;

namespace Unified.Scheduling.Models;

public record ShiftSeriesRequest : CalendarEventSeriesRequest
{
    internal bool PreserveLunchValues { get; init; }

    public int? LunchAvailableMinutes { get; set; }

    public int? WorkedLunchMinutes { get; set; }

    public IReadOnlyCollection<AssignmentSeriesLinkRequest> AssignmentSeriesLinks { get; init; } = [];
}
