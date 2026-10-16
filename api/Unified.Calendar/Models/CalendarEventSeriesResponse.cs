namespace Unified.Common.Calendar.Scheduling.Models;

public record CalendarEventSeriesResponse : CalendarEventSeriesRequest
{
    public int Id { get; init; }

    public string? EventTypeCode { get; init; }

    public string? StatusTypeCode { get; init; }

    public DateTimeOffset? CancelledAt { get; init; }

    public Guid? CancelledByUserId { get; init; }

    public string? CancellationReason { get; init; }
}
