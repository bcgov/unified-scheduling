using Unified.Common.Calendar.Scheduling.Models;

namespace Unified.Scheduling.Models;

public sealed record ShiftSeriesResponse : CalendarEventSeriesResponse
{
    public uint ConcurrencyToken { get; init; }

    public int? LunchAvailableMinutes { get; init; }

    public int? WorkedLunchMinutes { get; init; }

    public IReadOnlyCollection<int> ShiftEntryIds { get; init; } = [];
    public IReadOnlyCollection<ShiftAssignmentSeriesLinkSummaryResponse> AssignmentSeriesLinks { get; init; } = [];
}
