using Unified.Common.Calendar.Scheduling.Models;

namespace Unified.Scheduling.Models;

public sealed record ShiftSeriesResponse : CalendarEventSeriesResponse
{
    public uint ConcurrencyToken { get; init; }

    public Guid? CreatedById { get; init; }

    public DateTimeOffset? CreatedOn { get; init; }

    public Guid? UpdatedById { get; init; }

    public DateTimeOffset? UpdatedOn { get; init; }

    public int? LunchAvailableMinutes { get; init; }

    public int? WorkedLunchMinutes { get; init; }

    public IReadOnlyCollection<int> ShiftEntryIds { get; init; } = [];
    public IReadOnlyCollection<ShiftAssignmentSeriesLinkSummaryResponse> AssignmentSeriesLinks { get; init; } = [];
}
