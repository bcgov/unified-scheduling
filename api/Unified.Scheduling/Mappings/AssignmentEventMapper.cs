using Unified.Calendar.Mappings;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Mappings;

internal static class AssignmentEventMapper
{
    private static readonly CalendarEventMapper CalendarEventMapper = new();

    public static EventSeries ToEventSeries(AssignmentSeriesRequest request) =>
        CalendarEventMapper.ToEventSeries(request, SchedulingConstants.AssignmentEventTypeCode);

    public static void ApplyToEventSeries(EventSeries eventSeries, AssignmentSeriesRequest request) =>
        CalendarEventMapper.ApplyToEventSeries(eventSeries, request);

    public static Event ToEvent(AssignmentEntryRequest request, int? eventSeriesId)
    {
        var eventEntity = CalendarEventMapper.ToEvent(
            request,
            SchedulingConstants.SourceModule,
            SchedulingConstants.AssignmentEventTypeCode,
            eventSeriesId
        );

        return eventEntity;
    }

    public static void ApplyToEvent(Event eventEntity, AssignmentEntryUpdateRequest request) =>
        CalendarEventMapper.ApplyToEvent(eventEntity, request);
}
