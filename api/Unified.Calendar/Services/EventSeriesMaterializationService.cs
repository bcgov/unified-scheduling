using Mapster;
using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Db.Models.Calendar;

namespace Unified.Calendar.Services;

public sealed class EventSeriesMaterializationService(
    UnifiedDbContext db,
    IRecurrenceRuleValidator recurrenceRuleValidator,
    IRecurrenceExpander recurrenceExpander
) : IEventSeriesMaterializationService
{
    /// <summary>
    /// Create all event entries for a given event series, based on the recurrence rule and the start/end dates.
    /// </summary>
    /// <param name="eventSeries"></param>
    /// <param name="options"></param>
    /// <param name="handler"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<EventSeriesMaterializationResult> MaterializeAsync(
        EventSeries eventSeries,
        EventSeriesMaterializationOptions options,
        IEventSeriesMaterializationHandler handler,
        CancellationToken cancellationToken
    )
    {
        ValidateEventSeries(eventSeries, options);
        var context = await handler.CreateContextAsync(eventSeries, cancellationToken);

        return await MaterializeAsync(eventSeries, options, handler, context, cancellationToken);
    }

    /// <summary>
    /// Recreate all event entries for a given event series, based on the recurrence rule and the start/end dates.
    /// Only execute the recreate operation if the the event series is in a valid state.
    /// </summary>
    /// <param name="eventSeries"></param>
    /// <param name="options"></param>
    /// <param name="handler"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task<EventSeriesMaterializationResult> RecreateAsync(
        EventSeries eventSeries,
        EventSeriesMaterializationOptions options,
        IEventSeriesMaterializationHandler handler,
        CancellationToken cancellationToken
    )
    {
        ValidateEventSeries(eventSeries, options);
        var events = await db.Events.Where(e => e.EventSeriesId == eventSeries.Id).ToListAsync(cancellationToken);
        if (!await handler.CanRecreateSeriesEntriesAsync(eventSeries, events, cancellationToken))
            throw new InvalidOperationException("Event series entries cannot be recreated in the current state.");
        var context = await handler.CreateContextAsync(eventSeries, cancellationToken);

        var existingEvents = await db
            .Events.Include(e => e.EventSeries)
            .Where(e => e.EventSeriesId == eventSeries.Id)
            .Where(e => e.SourceModule == handler.SourceModule)
            .Where(e => e.EventTypeCode == handler.EventTypeCode)
            .ToListAsync(cancellationToken);

        foreach (var eventEntity in existingEvents)
        {
            await handler.OnEventRemovedAsync(eventSeries, eventEntity, context, cancellationToken);
        }

        db.Events.RemoveRange(existingEvents);
        await db.SaveChangesAsync(cancellationToken);

        var materializedResult = await MaterializeAsync(eventSeries, options, handler, context, cancellationToken);

        return materializedResult with
        {
            RemovedCount = existingEvents.Count,
            RemovedEventIds = existingEvents.Select(e => e.Id).ToList(),
        };
    }

    private async Task<EventSeriesMaterializationResult> MaterializeAsync(
        EventSeries eventSeries,
        EventSeriesMaterializationOptions options,
        IEventSeriesMaterializationHandler handler,
        IEventSeriesMaterializationContext context,
        CancellationToken cancellationToken
    )
    {
        var occurrences = ExpandOccurrences(eventSeries, options);

        var createdEvents = new List<Event>();
        foreach (var occurrence in occurrences)
        {
            var eventEntity = CreateMaterializedEvent(eventSeries, occurrence, handler);
            db.Events.Add(eventEntity);
            await handler.OnEventCreatedAsync(eventSeries, eventEntity, occurrence, context, cancellationToken);
            createdEvents.Add(eventEntity);
        }

        await db.SaveChangesAsync(cancellationToken);

        return new EventSeriesMaterializationResult
        {
            CreatedCount = createdEvents.Count,
            CreatedEventIds = createdEvents.Select(e => e.Id).ToList(),
        };
    }

    private void ValidateEventSeries(EventSeries eventSeries, EventSeriesMaterializationOptions options)
    {
        if (string.IsNullOrWhiteSpace(eventSeries.RecurrenceRule))
            throw new InvalidOperationException("Event series recurrence rule is required for materialization.");

        var validationResult = recurrenceRuleValidator.Validate(
            eventSeries.RecurrenceRule,
            eventSeries.StartAtUtc,
            eventSeries.EndAtUtc,
            eventSeries.TimeZoneId,
            options.ValidationOptions
        );

        if (!validationResult.IsValid)
            throw new InvalidOperationException(string.Join(Environment.NewLine, validationResult.Errors));
    }

    private IReadOnlyCollection<SeriesEntry> ExpandOccurrences(
        EventSeries eventSeries,
        EventSeriesMaterializationOptions options
    ) => recurrenceExpander.ExpandAllBounded(eventSeries, options.ValidationOptions.MaximumOccurrences);

    private static Event CreateMaterializedEvent(
        EventSeries eventSeries,
        SeriesEntry eventEntry,
        IEventSeriesMaterializationHandler handler
    )
    {
        var eventEntity = eventSeries.Adapt<Event>();
        eventEntity.Id = default;
        eventEntity.EventSeries = null;
        eventEntity.EventSeriesId = eventSeries.Id;
        eventEntity.EventType = null;
        eventEntity.StatusType = null;
        eventEntity.CancelledAt = null;
        eventEntity.CancelledByUserId = null;
        eventEntity.CancelledByUser = null;
        eventEntity.CancellationReason = null;
        eventEntity.Location = null;
        eventEntity.StartAtUtc = eventEntry.StartAtUtc;
        eventEntity.EndAtUtc = eventEntry.EndAtUtc;
        eventEntity.SeriesStartAtUtc = eventEntry.StartAtUtc;
        eventEntity.SeriesEndAtUtc = eventEntry.EndAtUtc;
        eventEntity.IsException = false;
        eventEntity.EventTypeCode = handler.EventTypeCode;
        eventEntity.SourceModule = handler.SourceModule;

        return eventEntity;
    }
}
