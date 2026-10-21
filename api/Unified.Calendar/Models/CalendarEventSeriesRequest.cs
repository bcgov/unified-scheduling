namespace Unified.Common.Calendar.Scheduling.Models;

public record CalendarEventSeriesRequest : CalendarEventBase
{
    public string? RecurrenceRule { get; init; }
}
