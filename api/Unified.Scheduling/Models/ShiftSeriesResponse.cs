using Unified.Common.Calendar.Scheduling.Models;

namespace Unified.Scheduling.Models;

public sealed record ShiftSeriesResponse : CalendarEventSeriesResponse
{
    public IReadOnlyCollection<int> ShiftEntryIds { get; init; } = [];
}
