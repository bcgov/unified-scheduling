using Unified.Db.Models.Calendar;

namespace Unified.Calendar.Services;

public interface IEventSeriesMaterializationService
{
    /// <summary>
    /// Performs initial materialization only and fails if active materialized events already exist.
    /// </summary>
    Task MaterializeAsync<TContext>(
        EventSeries eventSeries,
        RecurrenceValidationOptions validationOptions,
        IEventSeriesMaterializationHandler<TContext> handler,
        TContext context,
        CancellationToken cancellationToken
    )
        where TContext : notnull;

    /// <summary>
    /// Deletes and recreates draft materialized events in the tracked graph. The caller owns persistence.
    /// </summary>
    Task RegenerateDraftSeriesAsync<TContext>(
        EventSeries eventSeries,
        RecurrenceValidationOptions validationOptions,
        IEventSeriesMaterializationHandler<TContext> handler,
        TContext context,
        CancellationToken cancellationToken
    )
        where TContext : notnull;
}
