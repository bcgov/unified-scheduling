using Unified.Db.Models.Calendar;

namespace Unified.Calendar.Services;

public interface IEventSeriesMaterializationService
{
    Task<EventSeriesMaterializationResult> MaterializeAsync(
        EventSeries eventSeries,
        EventSeriesMaterializationOptions options,
        IEventSeriesMaterializationHandler handler,
        CancellationToken cancellationToken
    );

    Task<EventSeriesMaterializationResult> RecreateAsync(
        EventSeries eventSeries,
        EventSeriesMaterializationOptions options,
        IEventSeriesMaterializationHandler handler,
        CancellationToken cancellationToken
    );
}
