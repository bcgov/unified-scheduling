using Unified.Calendar.Factories;
using Unified.Calendar.Mappings;
using Unified.Calendar.Models;
using Unified.Common.Calendar.Scheduling.Models;
using Unified.Db.Models.Calendar;

namespace Unified.Calendar.Services;

public class CalendarEventSeriesService<TRequest, TEntity>(
    ICalendarEventSeriesFactory<TRequest, TEntity> factory,
    CalendarEventMapper calendarEventMapper,
    CalendarLifecycleService calendarLifecycleService
) : ICalendarEventService<TRequest, TEntity>
    where TRequest : CalendarEventSeriesRequest
{
    public TEntity BuildEvent(TRequest request, string sourceModule, string eventTypeCode, int? eventSeriesId = null)
    {
        //Map it to an EventSeries entity.
        var eventEntity = calendarEventMapper.ToEventSeries(request, eventTypeCode);

        //Build the calendar series using the factory and return it.
        return factory.CreateSeries(request, eventEntity);
    }

    /// <summary>
    /// Checks shared invariants, captures original parent values and detects recurrence changes,
    /// then maps the parent and calls the domain factory with that immutable update context.
    /// The domain caller validates and authorizes the request, loads required children/users/links,
    /// and captures previous domain state before calling this method. It must coordinate occurrence
    /// regeneration for recurrence changes or propagation preserving occurrence overrides otherwise,
    /// synchronize users/links, check conflicts, and save within its transaction. This method does
    /// not materialize occurrences or persist changes. The factory may use the context to prepare
    /// the domain update; it must not recalculate recurrence changes from the updated parent.
    /// </summary>
    public TEntity? UpdateEvent(TRequest request, TEntity existing, string sourceModule, string eventTypeCode)
    {
        if (existing is null)
            return default;

        var eventSeries = factory.GetEventSeries(existing);
        CalendarEventGuards.EnsureEventSeriesType(eventSeries, eventTypeCode);
        CalendarEventGuards.EnsureSeriesIsDraft(eventSeries);

        calendarEventMapper.ApplyToEventSeries(eventSeries, request);

        return factory.UpdateSeries(request, existing);
    }

    /// <summary>
    /// Returns true when <paramref name="entity"/>'s underlying series is still in a deletable
    /// (draft) lifecycle state. Domain-specific ownership/type checks are the caller's
    /// responsibility; this only enforces the shared lifecycle invariant.
    /// </summary>
    public bool IsDeletable(TEntity entity)
    {
        if (entity is null)
            return false;

        var eventSeries = factory.GetEventSeries(entity);
        if (!calendarLifecycleService.CanDelete(eventSeries))
            throw new InvalidOperationException(
                "Calendar series can only be deleted while all entries are in draft status."
            );
        return true;
    }
}
