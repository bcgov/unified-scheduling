namespace Unified.Common.Calendar.Scheduling.Models;

public record CalendarEventEntryRequest : CalendarEventBase
{
    public DateTimeOffset? SeriesStartAtUtc { get; init; }
    public DateTimeOffset? SeriesEndAtUtc { get; init; }
}
