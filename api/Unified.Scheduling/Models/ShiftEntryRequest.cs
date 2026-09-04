using Unified.Common.Calendar.Scheduling.Models;

namespace Unified.Scheduling.Models;

public record ShiftEntryRequest : CalendarEventEntryRequest
{
    public int? LunchAvailableMinutes { get; set; }

    public int? WorkedLunchMinutes { get; set; } = 0;

    public int? ShiftSeriesId { get; init; }

    public IReadOnlyCollection<AssignmentEntryLinkRequest>? AssignmentEntryLinks { get; init; }
}
