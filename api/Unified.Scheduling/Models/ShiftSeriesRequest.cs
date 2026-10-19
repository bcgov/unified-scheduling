using Unified.Common.Calendar.Scheduling.Models;

namespace Unified.Scheduling.Models;

public sealed record ShiftSeriesRequest : CalendarEventSeriesRequest
{
    public IReadOnlyCollection<AssignmentSeriesLinkRequest> AssignmentSeriesLinks { get; init; } = [];
}
