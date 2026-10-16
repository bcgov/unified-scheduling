using Microsoft.Extensions.Logging;
using Unified.Calendar.Factories;
using Unified.Calendar.Mappings;
using Unified.Common.Calendar.Scheduling.Models;
using Unified.Db.Models.Calendar;

namespace Unified.Calendar.Services;

public class CalendarEventEntryService<TRequest, TEntity>(
    ILogger<CalendarEventEntryService<TRequest, TEntity>> logger,
    ICalendarEventEntryFactory<TRequest, TEntity> factory,
    CalendarLifecycleService calendarLifecycleService,
    CalendarEventMapper calendarEventMapper
) : ICalendarEventService<TRequest, TEntity>
    where TRequest : CalendarEventEntryRequest
{
    public TEntity BuildEvent(TRequest request, string sourceModule, string eventTypeCode, int? eventSeriesId = null)
    {
        //Map it to an Event entity.
        var eventEntity = calendarEventMapper.ToEvent(request, sourceModule, eventTypeCode, eventSeriesId);
        //Update the exception flag for the event entity before building the entry.
        CalendarEventExceptionHelper.UpdateExceptionFlag(eventEntity);

        //Build the calendar entry using the factory and return it.
        return factory.CreateEntry(request, eventEntity);
    }

    public TEntity? UpdateEvent(TRequest request, TEntity existing, string sourceModule, string eventTypeCode)
    {
        if (existing is null)
        {
            logger.LogInformation("Calendar entry was not found for update.");
            return default;
        }

        var eventEntity = factory.GetEvent(existing);
        CalendarEventGuards.EnsureEventType(eventEntity, eventTypeCode, sourceModule);
        CalendarEventGuards.EnsureEntryIsDraft(eventEntity);

        calendarEventMapper.ApplyToEvent(eventEntity, request);
        CalendarEventExceptionHelper.UpdateExceptionFlag(eventEntity);

        return factory.UpdateEntry(request, existing);
    }

    /// <summary>
    /// Returns true when <paramref name="entity"/> is a standalone entry (not part of a series, which
    /// must be deleted through its series instead) and still in a deletable (draft) lifecycle state.
    /// Domain-specific ownership/type checks are the caller's responsibility; this only enforces the
    /// shared structural and lifecycle invariants.
    /// </summary>
    public bool IsDeletable(TEntity entity)
    {
        if (entity is null)
        {
            logger.LogInformation("Calendar entry was not found for delete.");
            return false;
        }

        var eventEntity = factory.GetEvent(entity);
        if (eventEntity.EventSeriesId.HasValue)
            throw new InvalidOperationException("Recurring calendar occurrences must be deleted through their series.");

        if (!calendarLifecycleService.CanDelete(eventEntity))
            throw new InvalidOperationException("Calendar entry can only be deleted while in draft status.");
        return true;
    }
}
