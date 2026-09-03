using Mapster;
using Unified.Db.Models.Calendar;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Mappings;

internal static class AssignmentEventMapper
{
    private static readonly TypeAdapterConfig MappingConfig = BuildMappingConfig();

    public static EventSeries ToEventSeries(AssignmentSeriesRequest request) =>
        request.Adapt(
            new EventSeries
            {
                EventTypeCode = SchedulingConstants.AssignmentEventTypeCode,
                StatusTypeCode = CalendarEventStatusTypeCodes.Draft,
            },
            MappingConfig
        );

    public static void ApplyToEventSeries(EventSeries eventSeries, AssignmentSeriesRequest request) =>
        request.Adapt(eventSeries, MappingConfig);

    public static Event ToEvent(AssignmentEntryRequest request, int? eventSeriesId) =>
        request.Adapt(
            new Event
            {
                EventSeriesId = eventSeriesId,
                IsException = false,
                EventTypeCode = SchedulingConstants.AssignmentEventTypeCode,
                StatusTypeCode = CalendarEventStatusTypeCodes.Draft,
                SourceModule = SchedulingConstants.SourceModule,
            },
            MappingConfig
        );

    public static void ApplyToEvent(Event eventEntity, AssignmentEntryUpdateRequest request) =>
        request.Adapt(eventEntity, MappingConfig);

    private static TypeAdapterConfig BuildMappingConfig()
    {
        var config = new TypeAdapterConfig();

        config
            .NewConfig<AssignmentSeriesRequest, EventSeries>()
            .Ignore(eventSeries => eventSeries.EventTypeCode)
            .Ignore(eventSeries => eventSeries.StatusTypeCode)
            .Map(eventSeries => eventSeries.Title, request => request.Title.Trim())
            .Map(
                eventSeries => eventSeries.Description,
                request => request.Description == null ? null : request.Description.Trim()
            )
            .Map(eventSeries => eventSeries.Notes, request => request.Notes == null ? null : request.Notes.Trim())
            .Map(eventSeries => eventSeries.Color, request => request.Color == null ? null : request.Color.Trim())
            .Map(
                eventSeries => eventSeries.TimeZoneId,
                request => request.TimeZoneId == null ? null : request.TimeZoneId.Trim()
            );

        config
            .NewConfig<AssignmentEntryRequest, Event>()
            .Ignore(eventEntity => eventEntity.EventSeriesId)
            .Ignore(eventEntity => eventEntity.IsException)
            .Ignore(eventEntity => eventEntity.EventTypeCode)
            .Ignore(eventEntity => eventEntity.StatusTypeCode)
            .Ignore(eventEntity => eventEntity.SourceModule)
            .Map(eventEntity => eventEntity.Title, request => request.Title.Trim())
            .Map(
                eventEntity => eventEntity.Description,
                request => request.Description == null ? null : request.Description.Trim()
            )
            .Map(eventEntity => eventEntity.Notes, request => request.Notes == null ? null : request.Notes.Trim())
            .Map(eventEntity => eventEntity.Color, request => request.Color == null ? null : request.Color.Trim())
            .Map(
                eventEntity => eventEntity.TimeZoneId,
                request => request.TimeZoneId == null ? null : request.TimeZoneId.Trim()
            );

        config
            .NewConfig<AssignmentEntryUpdateRequest, Event>()
            .Ignore(eventEntity => eventEntity.EventSeriesId)
            .Ignore(eventEntity => eventEntity.IsException)
            .Ignore(eventEntity => eventEntity.EventTypeCode)
            .Ignore(eventEntity => eventEntity.StatusTypeCode)
            .Ignore(eventEntity => eventEntity.SourceModule)
            .Map(eventEntity => eventEntity.Title, request => request.Title.Trim())
            .Map(
                eventEntity => eventEntity.Description,
                request => request.Description == null ? null : request.Description.Trim()
            )
            .Map(eventEntity => eventEntity.Notes, request => request.Notes == null ? null : request.Notes.Trim())
            .Map(eventEntity => eventEntity.Color, request => request.Color == null ? null : request.Color.Trim())
            .Map(
                eventEntity => eventEntity.TimeZoneId,
                request => request.TimeZoneId == null ? null : request.TimeZoneId.Trim()
            );

        return config;
    }
}
