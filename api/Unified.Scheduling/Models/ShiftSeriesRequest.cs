using Unified.Common.Calendar.Scheduling.Models;

namespace Unified.Scheduling.Models;

public record ShiftSeriesRequest : CalendarEventSeriesRequest
{
    public IReadOnlyCollection<AssignmentSeriesLinkRequest> AssignmentSeriesLinks { get; init; } = [];
}
