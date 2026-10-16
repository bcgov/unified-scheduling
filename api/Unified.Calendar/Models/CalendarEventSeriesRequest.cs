namespace Unified.Common.Calendar.Scheduling.Models;

public record CalendarEventSeriesRequest : CalendarEventBase
{
    public string? RecurrenceRule { get; init; }

    public int EventSeriesId { get; init; }

    public IReadOnlyCollection<int> EventIds { get; init; } = [];
}
