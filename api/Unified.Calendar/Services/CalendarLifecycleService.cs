using Unified.Db.Models.Calendar;

namespace Unified.Calendar.Services;

public sealed class CalendarLifecycleService
{
    public void Publish(Event eventEntity)
    {
        EnsureDraft(eventEntity.StatusTypeCode, "Calendar event");
        eventEntity.StatusTypeCode = CalendarEventStatusTypeCodes.Active;
    }

    public void PublishSeries(EventSeries eventSeries, IReadOnlyCollection<Event> childEvents)
    {
        Publish(eventSeries);
        foreach (var childEvent in childEvents)
        {
            if (childEvent.StatusTypeCode == CalendarEventStatusTypeCodes.Draft)
                Publish(childEvent);
        }
    }

    public void Cancel(
        Event eventEntity,
        DateTimeOffset cancelledAt,
        Guid? cancelledByUserId,
        string? cancellationReason
    )
    {
        eventEntity.StatusTypeCode = CalendarEventStatusTypeCodes.Cancelled;
        eventEntity.CancelledAt = cancelledAt;
        eventEntity.CancelledByUserId = cancelledByUserId;
        eventEntity.CancellationReason = cancellationReason?.Trim();
    }

    public void CancelSeries(
        EventSeries eventSeries,
        IReadOnlyCollection<Event> childEvents,
        DateTimeOffset cancelledAt,
        Guid? cancelledByUserId,
        string? cancellationReason
    )
    {
        Cancel(eventSeries, cancelledAt, cancelledByUserId, cancellationReason);
        foreach (var childEvent in childEvents)
        {
            if (childEvent.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled)
                Cancel(childEvent, cancelledAt, cancelledByUserId, cancellationReason);
        }
    }

    public bool CanDelete(Event eventEntity)
    {
        return eventEntity.StatusTypeCode == CalendarEventStatusTypeCodes.Draft;
    }

    public bool CanDelete(EventSeries eventSeries)
    {
        return eventSeries.StatusTypeCode == CalendarEventStatusTypeCodes.Draft;
    }

    private static void Publish(EventSeries eventSeries)
    {
        EnsureDraft(eventSeries.StatusTypeCode, "Calendar event series");
        eventSeries.StatusTypeCode = CalendarEventStatusTypeCodes.Active;
    }

    private static void Cancel(
        EventSeries eventSeries,
        DateTimeOffset cancelledAt,
        Guid? cancelledByUserId,
        string? cancellationReason
    )
    {
        eventSeries.StatusTypeCode = CalendarEventStatusTypeCodes.Cancelled;
        eventSeries.CancelledAt = cancelledAt;
        eventSeries.CancelledByUserId = cancelledByUserId;
        eventSeries.CancellationReason = cancellationReason?.Trim();
    }

    private static void EnsureDraft(string statusTypeCode, string entityName)
    {
        if (statusTypeCode != CalendarEventStatusTypeCodes.Draft)
            throw new InvalidOperationException($"{entityName} must be in draft status to publish.");
    }
}
