namespace Unified.Calendar.Models;

public sealed class CalendarEventResponse
{
    public required int Id { get; init; }

    public int? EventSeriesId { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public string? Notes { get; init; }

    public string? Color { get; init; } // Not a hex code, but a unique color name or identifier (e.g., "blue", "red", "green"). The client can map this to a hex code as needed.

    public required DateTimeOffset StartAtUtc { get; init; }

    public DateTimeOffset? EndAtUtc { get; init; }

    public DateTimeOffset? SeriesStartAtUtc { get; init; }

    public DateTimeOffset? SeriesEndAtUtc { get; init; }

    public string? TimeZoneId { get; init; }

    public bool AllDay { get; init; }

    public bool IsException { get; init; }

    public required CalendarEventTypeCode EventTypeCode { get; init; }

    public required CalendarEventStatusTypeCode StatusTypeCode { get; init; }

    public DateTimeOffset? CancelledAt { get; init; }

    public Guid? CancelledByUserId { get; init; }

    public string? CancellationReason { get; init; }

    public required string SourceModule { get; init; }

    public int? LocationId { get; init; }
}
