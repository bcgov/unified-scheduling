using Unified.Db.Models.Calendar;

namespace Unified.Calendar.Services;

/// <summary>
/// Owns module-specific records associated with materialized calendar events. Calendar owns recurrence while
/// handlers remove and create module-owned records in the caller's tracked graph.
/// </summary>
public interface IEventSeriesMaterializationHandler<TContext>
    where TContext : notnull
{
    string SourceModule { get; }

    string EventTypeCode { get; }

    /// <summary>Removes module-owned records before their materialized events are deleted.</summary>
    Task OnMaterializedEventsDeletingAsync(
        EventSeries eventSeries,
        IReadOnlyCollection<Event> existingEvents,
        TContext context,
        CancellationToken cancellationToken
    );

    /// <summary>Creates module-owned records for a newly materialized calendar event.</summary>
    Task OnMaterializedEventCreatedAsync(
        EventSeries eventSeries,
        Event eventEntity,
        SeriesEntry occurrence,
        TContext context,
        CancellationToken cancellationToken
    );
}
