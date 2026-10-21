using Unified.Common.Calendar.Scheduling.Models;
using Unified.Db.Models.Calendar;

namespace Unified.Calendar.Services;

public interface ICalendarEventService<TRequest, TEntity, TCalendarEntity>
    where TRequest : CalendarEventBase
    where TCalendarEntity : class
{
    /// <summary>
    Task<TEntity> BuildEventAsync(
        TRequest request,
        string sourceModule,
        string eventTypeCode,
        int? eventSeriesId = null,
        Func<TRequest, CancellationToken, Task>[]? validators = null,
        Action<TRequest>? guards = null,
        Action<TCalendarEntity>? overrides = null,
        CancellationToken cancellationToken = default
    );

    Task<TEntity?> UpdateEventAsync<TUpdateRequest>(
        TUpdateRequest request,
        TEntity? existing,
        string sourceModule,
        string eventTypeCode,
        Func<TUpdateRequest, CancellationToken, Task>[]? validators = null,
        Action<TUpdateRequest>? additionalGuards = null,
        Action<TCalendarEntity>? overrides = null,
        CancellationToken cancellationToken = default
    )
        where TUpdateRequest : TRequest;
    bool IsDeletable(TEntity? entity, string sourceModule, string eventTypeCode);
}
