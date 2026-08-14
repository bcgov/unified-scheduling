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
    public async Task MaterializeAsync<TContext>(
        EventSeries eventSeries,
        RecurrenceValidationOptions validationOptions,
        IEventSeriesMaterializationHandler<TContext> handler,
        TContext context,
        CancellationToken cancellationToken
    )
        where TContext : notnull
    {
        ValidateEventSeries(eventSeries, validationOptions);
        if (await HasActiveMaterializedEventsAsync(eventSeries, handler, cancellationToken))
            throw new InvalidOperationException(
                "Event series has already been materialized. Drop existing materialized events before materializing again."
            );

        await CreateMaterializedEventsAsync(eventSeries, validationOptions, handler, context, cancellationToken);
    }

    public async Task RegenerateDraftSeriesAsync<TContext>(
        EventSeries eventSeries,
        RecurrenceValidationOptions validationOptions,
        IEventSeriesMaterializationHandler<TContext> handler,
        TContext context,
        CancellationToken cancellationToken
    )
        where TContext : notnull
    {
        ValidateEventSeries(eventSeries, validationOptions);
        var existingEvents = await GetActiveMaterializedEventsAsync(eventSeries, handler, cancellationToken);
        if (eventSeries.StatusTypeCode != CalendarEventStatusTypeCodes.Draft)
            throw new InvalidOperationException("Only draft event series can be regenerated.");
        if (existingEvents.Any(eventEntity => eventEntity.StatusTypeCode != CalendarEventStatusTypeCodes.Draft))
            throw new InvalidOperationException("Materialized event entries cannot be recreated in the current state.");

        await handler.OnMaterializedEventsDeletingAsync(eventSeries, existingEvents, context, cancellationToken);
        db.Events.RemoveRange(existingEvents);

        await CreateMaterializedEventsAsync(eventSeries, validationOptions, handler, context, cancellationToken);
    }

    private async Task CreateMaterializedEventsAsync<TContext>(
        EventSeries eventSeries,
        RecurrenceValidationOptions validationOptions,
        IEventSeriesMaterializationHandler<TContext> handler,
        TContext context,
        CancellationToken cancellationToken
    )
        where TContext : notnull
    {
        var occurrences = ExpandOccurrences(eventSeries, validationOptions);

        foreach (var occurrence in occurrences)
        {
            var eventEntity = CreateMaterializedEvent(eventSeries, occurrence, handler);
            db.Events.Add(eventEntity);
            await handler.OnMaterializedEventCreatedAsync(
                eventSeries,
                eventEntity,
                occurrence,
                context,
                cancellationToken
            );
        }
    }

    private void ValidateEventSeries(EventSeries eventSeries, RecurrenceValidationOptions validationOptions)
    {
        if (string.IsNullOrWhiteSpace(eventSeries.RecurrenceRule))
            throw new InvalidOperationException("Event series recurrence rule is required for materialization.");
        if (!eventSeries.EndAtUtc.HasValue)
            throw new InvalidOperationException("Event series end date and time are required for materialization.");

        var validationResult = recurrenceRuleValidator.Validate(
            eventSeries.RecurrenceRule,
            eventSeries.StartAtUtc,
            eventSeries.EndAtUtc,
            eventSeries.TimeZoneId,
            validationOptions
        );

        if (!validationResult.IsValid)
            throw new InvalidOperationException(string.Join(Environment.NewLine, validationResult.Errors));
    }

    private IReadOnlyCollection<SeriesEntry> ExpandOccurrences(
        EventSeries eventSeries,
        RecurrenceValidationOptions validationOptions
    )
    {
        var occurrences = recurrenceExpander.ExpandAllBounded(eventSeries, validationOptions.MaximumOccurrences);
        if (occurrences.Any(occurrence => !occurrence.EndAtUtc.HasValue))
            throw new InvalidOperationException("Materialized recurring events require an end date and time.");

        return occurrences;
    }

    private Task<bool> HasActiveMaterializedEventsAsync<TContext>(
        EventSeries eventSeries,
        IEventSeriesMaterializationHandler<TContext> handler,
        CancellationToken cancellationToken
    )
        where TContext : notnull =>
        db.Events.AnyAsync(
            eventEntity =>
                eventEntity.EventSeriesId == eventSeries.Id
                && eventEntity.SourceModule == handler.SourceModule
                && eventEntity.EventTypeCode == handler.EventTypeCode
                && eventEntity.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled,
            cancellationToken
        );

    private Task<List<Event>> GetActiveMaterializedEventsAsync<TContext>(
        EventSeries eventSeries,
        IEventSeriesMaterializationHandler<TContext> handler,
        CancellationToken cancellationToken
    )
        where TContext : notnull =>
        db
            .Events.Where(eventEntity => eventEntity.EventSeriesId == eventSeries.Id)
            .Where(eventEntity => eventEntity.SourceModule == handler.SourceModule)
            .Where(eventEntity => eventEntity.EventTypeCode == handler.EventTypeCode)
            .Where(eventEntity => eventEntity.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled)
            .ToListAsync(cancellationToken);

    private static Event CreateMaterializedEvent<TContext>(
        EventSeries eventSeries,
        SeriesEntry eventEntry,
        IEventSeriesMaterializationHandler<TContext> handler
    )
        where TContext : notnull
    {
        var eventEntity = new Event();
        ApplyMaterializedEvent(eventSeries, eventEntry, handler, eventEntity);

        return eventEntity;
    }

    private static void ApplyMaterializedEvent<TContext>(
        EventSeries eventSeries,
        SeriesEntry eventEntry,
        IEventSeriesMaterializationHandler<TContext> handler,
        Event eventEntity
    )
        where TContext : notnull
    {
        eventEntity.Title = eventSeries.Title;
        eventEntity.Description = eventSeries.Description;
        eventEntity.Notes = eventSeries.Notes;
        eventEntity.Color = eventSeries.Color;
        eventEntity.LocationId = eventSeries.LocationId;
        eventEntity.TimeZoneId = eventSeries.TimeZoneId;
        eventEntity.AllDay = eventSeries.AllDay;
        eventEntity.CancelledAt = null;
        eventEntity.CancelledByUserId = null;
        eventEntity.CancellationReason = null;
        eventEntity.StartAtUtc = eventEntry.StartAtUtc;
        eventEntity.EndAtUtc = eventEntry.EndAtUtc;
        eventEntity.SeriesStartAtUtc = eventEntry.StartAtUtc;
        eventEntity.SeriesEndAtUtc = eventEntry.EndAtUtc;
        eventEntity.IsException = false;
        eventEntity.EventSeries = eventSeries;
        eventEntity.EventTypeCode = handler.EventTypeCode;
        eventEntity.StatusTypeCode = eventSeries.StatusTypeCode;
        eventEntity.SourceModule = handler.SourceModule;
    }
}
