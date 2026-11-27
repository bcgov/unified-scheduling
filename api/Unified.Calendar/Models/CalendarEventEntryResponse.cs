using Unified.Calendar;

namespace Unified.Common.Calendar.Scheduling.Models;

public record CalendarEventEntryResponse : CalendarEventEntryRequest
{
    public int Id { get; init; }

    public int? EventSeriesId { get; init; }

    public bool IsException { get; init; }

    public IReadOnlyCollection<CalendarEventExceptionReasonCode> ExceptionReasonCodes { get; init; } = [];

    public string? EventTypeCode { get; init; }

    public string? StatusTypeCode { get; init; }

    public DateTimeOffset? CancelledAt { get; init; }

    public Guid? CancelledByUserId { get; init; }

    public string? CancellationReason { get; init; }

    public Guid? CreatedById { get; init; }

    public DateTimeOffset? CreatedOn { get; init; }

    public Guid? UpdatedById { get; init; }

    public DateTimeOffset? UpdatedOn { get; init; }
}
