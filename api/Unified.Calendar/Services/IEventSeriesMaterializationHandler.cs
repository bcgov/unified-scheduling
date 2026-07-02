using Unified.Db.Models.Calendar;

namespace Unified.Calendar.Services;

public interface IEventSeriesMaterializationHandler
{
    string SourceModule { get; }

    string EventTypeCode { get; }

    Task<IEventSeriesMaterializationContext> CreateContextAsync(
        EventSeries eventSeries,
        CancellationToken cancellationToken
    );

    Task<bool> CanRecreateSeriesEntriesAsync(
        EventSeries eventSeries,
        IEnumerable<Event> events,
        CancellationToken cancellationToken
    );

    Task OnEventCreatedAsync(
        EventSeries eventSeries,
        Event eventEntity,
        SeriesEntry occurrence,
        IEventSeriesMaterializationContext context,
        CancellationToken cancellationToken
    );

    Task OnEventUpdatedAsync(
        EventSeries eventSeries,
        Event eventEntity,
        SeriesEntry occurrence,
        IEventSeriesMaterializationContext context,
        CancellationToken cancellationToken
    );

    Task OnEventRemovedAsync(
        EventSeries eventSeries,
        Event eventEntity,
        IEventSeriesMaterializationContext context,
        CancellationToken cancellationToken
    );
}
