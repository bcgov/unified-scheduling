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
) : ICalendarEventService<TRequest, TEntity, Event>
    where TRequest : CalendarEventEntryRequest
    where TEntity : class
{
    public async Task<TEntity> BuildEventAsync(
        TRequest request,
        string sourceModule,
        string eventTypeCode,
        int? eventSeriesId = null,
        Func<TRequest, CancellationToken, Task>[]? validators = null,
        Action<TRequest>? guards = null,
        Action<Event>? overrides = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        await RunValidationAsync(request, validators, guards, cancellationToken);
        var eventEntity = calendarEventMapper.ToEvent(request, sourceModule, eventTypeCode, eventSeriesId);
        overrides?.Invoke(eventEntity);
        CalendarEventExceptionHelper.UpdateExceptionFlag(eventEntity);

        //Build the calendar entry using the factory and return it.
        return factory.CreateEntry(request, eventEntity);
    }

    /// <summary>
    public async Task<TEntity?> UpdateEventAsync<TUpdateRequest>(
        TUpdateRequest request,
        TEntity? existing,
        string sourceModule,
        string eventTypeCode,
        Func<TUpdateRequest, CancellationToken, Task>[]? validators = null,
        Action<TUpdateRequest>? additionalGuards = null,
        Action<Event>? overrides = null,
        CancellationToken cancellationToken = default
    )
        where TUpdateRequest : TRequest
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (existing is null)
        {
            logger.LogInformation("Calendar entry was not found for update.");
            return null;
        }

        var eventEntity = factory.GetEvent(existing);
        CalendarEventGuards.EnsureEventType(eventEntity, eventTypeCode, sourceModule);
        CalendarEventGuards.EnsureEntryIsDraft(eventEntity);

        await RunValidationAsync(request, validators, additionalGuards, cancellationToken);
        calendarEventMapper.ApplyToEvent(eventEntity, request);
        overrides?.Invoke(eventEntity);
        CalendarEventExceptionHelper.UpdateExceptionFlag(eventEntity);

        return factory.UpdateEntry<TUpdateRequest>(request, existing);
    }

    /// <summary>
    /// Returns true when <paramref name="entity"/> is a standalone entry (not part of a series, which
    /// must be deleted through its series instead) and still in a deletable (draft) lifecycle state.
    /// Domain-specific ownership/type checks are the caller's responsibility; this only enforces the
    /// shared structural and lifecycle invariants.
    /// </summary>
    public bool IsDeletable(TEntity? entity, string sourceModule, string eventTypeCode)
    {
        if (entity is null)
        {
            logger.LogInformation("Calendar entry was not found for delete.");
            return false;
        }

        var eventEntity = factory.GetEvent(entity);
        CalendarEventGuards.EnsureEventType(eventEntity, eventTypeCode, sourceModule);
        CalendarEventGuards.EnsureEntryIsDraft(eventEntity);

        return true;
    }

    private static async Task RunValidationAsync<TInput>(
        TInput request,
        Func<TInput, CancellationToken, Task>[]? validators,
        Action<TInput>? additionalGuards,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (validators is not null)
        {
            foreach (var validate in validators)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await validate(request, cancellationToken);
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        additionalGuards?.Invoke(request);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
