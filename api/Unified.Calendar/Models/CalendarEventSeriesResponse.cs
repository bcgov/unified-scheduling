namespace Unified.Common.Calendar.Scheduling.Models;

public record CalendarEventSeriesResponse : CalendarEventSeriesRequest
{
    public int Id { get; init; }

    public int EventSeriesId { get; init; }
    public string? EventTypeCode { get; init; }

    public string? StatusTypeCode { get; init; }

    public DateTimeOffset? CancelledAt { get; init; }

    public Guid? CancelledByUserId { get; init; }

    public string? CancellationReason { get; init; }
    public IReadOnlyCollection<int> EventIds { get; init; } = [];

    public Guid? CreatedById { get; init; }

    public DateTimeOffset? CreatedOn { get; init; }

    public Guid? UpdatedById { get; init; }

    public DateTimeOffset? UpdatedOn { get; init; }
}
