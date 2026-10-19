using Unified.Calendar.Factories;
using Unified.Calendar.Mappings;
using Unified.Common.Calendar.Scheduling.Models;
using Unified.Db.Models.Calendar;

namespace Unified.Calendar.Services;

public class CalendarEventSeriesService<TRequest, TEntity>(
    ICalendarEventSeriesFactory<TRequest, TEntity> factory,
    CalendarEventMapper calendarEventMapper,
    CalendarLifecycleService calendarLifecycleService
) : ICalendarEventSeriesService<TRequest, TEntity>
    where TRequest : CalendarEventSeriesRequest
    where TEntity : class
{
    public async Task<TEntity> BuildEventAsync(
        TRequest request,
        string sourceModule,
        string eventTypeCode,
        int? eventSeriesId = null,
        Func<TRequest, CancellationToken, Task>[]? validators = null,
        Action<TRequest>? guards = null,
        Action<EventSeries>? overrides = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        await RunValidationAsync(request, validators, guards, cancellationToken);
        var eventSeries = calendarEventMapper.ToEventSeries(request, eventTypeCode);
        overrides?.Invoke(eventSeries);
        cancellationToken.ThrowIfCancellationRequested();
        return factory.CreateSeries(request, eventSeries);
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
    public async Task<TEntity?> UpdateEventAsync<TUpdateRequest>(
        TUpdateRequest request,
        TEntity? existing,
        string sourceModule,
        string eventTypeCode,
        Func<TUpdateRequest, CancellationToken, Task>[]? validators = null,
        Action<TUpdateRequest>? additionalGuards = null,
        Action<EventSeries>? overrides = null,
        CancellationToken cancellationToken = default
    )
        where TUpdateRequest : TRequest
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (existing is null)
            return null;

        var eventSeries = factory.GetEventSeries(existing);
        CalendarEventGuards.EnsureEventSeriesType(eventSeries, eventTypeCode);
        CalendarEventGuards.EnsureSeriesIsDraft(eventSeries);

        await RunValidationAsync(request, validators, additionalGuards, cancellationToken);
        overrides?.Invoke(eventSeries);
        calendarEventMapper.ApplyToEventSeries(eventSeries, request);

        return factory.UpdateSeries(request, existing);
    }

    public bool HasRecurrenceChanged(TEntity entity, TRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (entity is null)
            return false;
        return factory.HasRecurrenceChanged(factory.GetEventSeries(entity), request);
    }

    /// <summary>
    /// Returns true when <paramref name="entity"/>'s underlying series is still in a deletable
    /// (draft) lifecycle state. Domain-specific ownership/type checks are the caller's
    /// responsibility; this only enforces the shared lifecycle invariant.
    /// </summary>
    public bool IsDeletable(TEntity? entity, string sourceModule, string eventTypeCode)
    {
        if (entity is null)
            return false;

        var eventSeries = factory.GetEventSeries(entity);
        CalendarEventGuards.EnsureEventSeriesType(eventSeries, eventTypeCode);
        if (!calendarLifecycleService.CanDelete(eventSeries))
            throw new InvalidOperationException(
                "Calendar series can only be deleted while all entries are in draft status."
            );
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
