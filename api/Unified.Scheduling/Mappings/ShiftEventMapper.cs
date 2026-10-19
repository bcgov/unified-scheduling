using Unified.Calendar.Mappings;
using Unified.Db.Models.Calendar;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Mappings;

internal static class ShiftEventMapper
{
    private static readonly CalendarEventMapper calendarEventMapper = new CalendarEventMapper();

    public static EventSeries ToEventSeries(ShiftSeriesRequest request) =>
        calendarEventMapper.ToEventSeries(request, SchedulingConstants.ShiftEventTypeCode);

    public static void ApplyToEventSeries(EventSeries eventSeries, ShiftSeriesRequest request) =>
        calendarEventMapper.ApplyToEventSeries(eventSeries, request);

    public static Event ToEvent(ShiftEntryRequest request, int? eventSeriesId)
    {
        var eventEntity = calendarEventMapper.ToEvent(
            request,
            SchedulingConstants.SourceModule,
            SchedulingConstants.ShiftEventTypeCode,
            eventSeriesId
        );
        eventEntity.SeriesStartAtUtc = request.SeriesStartAtUtc;
        eventEntity.SeriesEndAtUtc = request.SeriesEndAtUtc;

        return eventEntity;
    }

    public static void ApplyToEvent(Event eventEntity, ShiftEntryRequest request, int? eventSeriesId)
    {
        calendarEventMapper.ApplyToEvent(eventEntity, request);
        eventEntity.EventSeriesId = eventSeriesId;
    }
}
