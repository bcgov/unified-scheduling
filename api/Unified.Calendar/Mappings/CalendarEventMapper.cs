using Mapster;
using Unified.Common.Calendar.Scheduling.Models;
using Unified.Db.Models.Calendar;

namespace Unified.Calendar.Mappings;

/// <summary>
/// Translates the common <see cref="CalendarEventBase"/>/<see cref="CalendarEventSeriesRequest"/>
/// request base onto the calendar's <c>Event</c>/<c>EventSeries</c> entities, for use by
/// <see cref="CalendarEventEntryService{TRequest,TEntity}"/> and
/// <see cref="CalendarEventSeriesService{TRequest,TEntity}"/> across any domain built on the shared
/// calendar pipeline. Unlike the per-feature mappers it sits alongside (e.g.
/// <c>Unified.Scheduling.Mappings.AssignmentEventMapper</c>/<c>ShiftEventMapper</c> and
/// <c>Unified.TimeOff.Mappings.SchedulingTimeOffEventMapper</c>, none of which share a base config),
/// this class owns the translation itself rather than exposing an extensible
/// <see cref="TypeAdapterConfig"/>, because it is the one piece those domains have in common; each
/// factory layers its own domain-specific fields on afterwards in <c>CreateEntry</c>/<c>CreateSeries</c>.
/// </summary>
public sealed class CalendarEventMapper
{
    private static readonly TypeAdapterConfig MappingConfig = BuildMappingConfig();

    public Event ToEvent<TRequest>(
        TRequest request,
        string sourceModule,
        string eventTypeCode,
        int? eventSeriesId = null
    )
        where TRequest : CalendarEventEntryRequest
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Adapt(
            new Event
            {
                EventSeriesId = eventSeriesId,
                IsException = false,
                EventTypeCode = eventTypeCode,
                StatusTypeCode = CalendarEventStatusTypeCodes.Draft,
                SourceModule = sourceModule,
                SeriesStartAtUtc = request.SeriesStartAtUtc,
                SeriesEndAtUtc = request.SeriesEndAtUtc,
            },
            MappingConfig
        );
    }

    /// <summary>
    /// Re-applies the shared, mutable fields of <paramref name="request"/> onto an existing
    /// <paramref name="eventEntity"/> for update operations. Identity fields (series linkage, type
    /// code, status, source module, series baseline times) are intentionally left untouched here.
    /// </summary>
    public void ApplyToEvent<TRequest>(Event eventEntity, TRequest request)
        where TRequest : CalendarEventBase
    {
        ArgumentNullException.ThrowIfNull(eventEntity);
        ArgumentNullException.ThrowIfNull(request);

        request.Adapt(eventEntity, MappingConfig);
    }

    /// <summary>
    /// Maps the common calendar <c>Event</c> fields onto a <typeparamref name="TResponse"/> produced
    /// by <paramref name="seed"/>. <paramref name="seed"/> only needs to satisfy the compile-time
    /// <c>required</c> members of <typeparamref name="TResponse"/> (e.g. with placeholder values) -
    /// every shared field is immediately overwritten below via a record <c>with</c> expression, which
    /// preserves the actual derived runtime type. Domain-specific identity/extra fields (e.g. the
    /// owning entry's own <c>Id</c>) are left untouched and should be set by the caller afterwards.
    /// </summary>
    public TResponse ToEntryResponse<TResponse>(
        Func<TResponse> seed,
        Event eventEntity,
        IReadOnlyCollection<Guid> userIds
    )
        where TResponse : CalendarEventEntryResponse
    {
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentNullException.ThrowIfNull(eventEntity);

        return seed() with
        {
            EventSeriesId = eventEntity.EventSeriesId,
            EventTypeCode = eventEntity.EventTypeCode,
            StatusTypeCode = eventEntity.StatusTypeCode,
            CancelledAt = eventEntity.CancelledAt,
            CancelledByUserId = eventEntity.CancelledByUserId,
            CancellationReason = eventEntity.CancellationReason,
            Title = eventEntity.Title,
            Description = eventEntity.Description,
            Notes = eventEntity.Notes,
            Color = eventEntity.Color,
            StartAtUtc = eventEntity.StartAtUtc,
            EndAtUtc = eventEntity.EndAtUtc,
            TimeZoneId = eventEntity.TimeZoneId,
            AllDay = eventEntity.AllDay,
            LocationId = eventEntity.LocationId,
            UserIds = userIds,
            SeriesStartAtUtc = eventEntity.SeriesStartAtUtc,
            SeriesEndAtUtc = eventEntity.SeriesEndAtUtc,
        };
    }

    /// <summary>
    /// Maps the common calendar <c>EventSeries</c> fields onto a <typeparamref name="TResponse"/>
    /// produced by <paramref name="seed"/>. See <see cref="ToEntryResponse{TResponse}"/> for why a
    /// seed factory is required instead of a <c>new()</c> constraint. <c>EventSeriesId</c> identifies
    /// the calendar series; <c>Id</c> identifies the owning domain entity and is preserved from the seed.
    /// </summary>
    public TResponse ToSeriesResponse<TResponse>(
        Func<TResponse> seed,
        EventSeries eventSeries,
        IReadOnlyCollection<Guid> userIds,
        IReadOnlyCollection<int> eventIds
    )
        where TResponse : CalendarEventSeriesResponse
    {
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentNullException.ThrowIfNull(eventSeries);

        return seed() with
        {
            EventSeriesId = eventSeries.Id,
            EventTypeCode = eventSeries.EventTypeCode,
            StatusTypeCode = eventSeries.StatusTypeCode,
            CancelledAt = eventSeries.CancelledAt,
            CancelledByUserId = eventSeries.CancelledByUserId,
            CancellationReason = eventSeries.CancellationReason,
            Title = eventSeries.Title,
            Description = eventSeries.Description,
            Notes = eventSeries.Notes,
            Color = eventSeries.Color,
            RecurrenceRule = eventSeries.RecurrenceRule,
            TimeZoneId = eventSeries.TimeZoneId,
            StartAtUtc = eventSeries.StartAtUtc,
            EndAtUtc = eventSeries.EndAtUtc,
            AllDay = eventSeries.AllDay,
            LocationId = eventSeries.LocationId,
            UserIds = userIds,
            EventIds = eventIds,
        };
    }

    public EventSeries ToEventSeries<TRequest>(TRequest request, string eventTypeCode)
        where TRequest : CalendarEventSeriesRequest
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Adapt(
            new EventSeries { EventTypeCode = eventTypeCode, StatusTypeCode = CalendarEventStatusTypeCodes.Draft },
            MappingConfig
        );
    }

    /// <summary>
    /// Re-applies the shared, mutable fields of <paramref name="request"/> onto an existing
    /// <paramref name="eventSeries"/> for update operations. Identity fields (type code, status) are
    /// intentionally left untouched here.
    /// </summary>
    public void ApplyToEventSeries<TRequest>(EventSeries eventSeries, TRequest request)
        where TRequest : CalendarEventSeriesRequest
    {
        ArgumentNullException.ThrowIfNull(eventSeries);
        ArgumentNullException.ThrowIfNull(request);

        request.Adapt(eventSeries, MappingConfig);
    }

    private static TypeAdapterConfig BuildMappingConfig()
    {
        var config = new TypeAdapterConfig();

        config
            .NewConfig<CalendarEventBase, Event>()
            .Ignore(dest => dest.EventSeriesId)
            .Ignore(dest => dest.IsException)
            .Ignore(dest => dest.EventTypeCode)
            .Ignore(dest => dest.StatusTypeCode)
            .Ignore(dest => dest.SourceModule)
            .Ignore(dest => dest.SeriesStartAtUtc)
            .Ignore(dest => dest.SeriesEndAtUtc)
            .Map(dest => dest.Title, src => src.Title.Trim())
            .Map(dest => dest.Description, src => src.Description == null ? null : src.Description.Trim())
            .Map(dest => dest.Notes, src => src.Notes == null ? null : src.Notes.Trim())
            .Map(dest => dest.Color, src => src.Color == null ? null : src.Color.Trim())
            .Map(dest => dest.TimeZoneId, src => src.TimeZoneId == null ? null : src.TimeZoneId.Trim());

        config
            .NewConfig<CalendarEventSeriesRequest, EventSeries>()
            .Ignore(dest => dest.EventTypeCode)
            .Ignore(dest => dest.StatusTypeCode)
            .Map(dest => dest.Title, src => src.Title.Trim())
            .Map(dest => dest.Description, src => src.Description == null ? null : src.Description.Trim())
            .Map(dest => dest.Notes, src => src.Notes == null ? null : src.Notes.Trim())
            .Map(dest => dest.Color, src => src.Color == null ? null : src.Color.Trim())
            .Map(dest => dest.TimeZoneId, src => src.TimeZoneId == null ? null : src.TimeZoneId.Trim())
            .Map(dest => dest.RecurrenceRule, src => src.RecurrenceRule);

        return config;
    }
}
