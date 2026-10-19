using Unified.Common.Calendar.Scheduling.Models;

namespace Unified.Scheduling.Models;

public record ShiftEntryRequest : CalendarEventEntryRequest
{
    public int? ShiftSeriesId { get; init; }
    public DateTimeOffset? SeriesStartAtUtc { get; init; }

    public DateTimeOffset? SeriesEndAtUtc { get; init; }
    public IReadOnlyCollection<AssignmentEntryLinkRequest>? AssignmentEntryLinks { get; init; }
}
