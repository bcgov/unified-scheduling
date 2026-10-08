using Unified.Db.Models.Calendar;
using Unified.TimeOff.Constants;
using Unified.TimeOff.Models;
using Unified.TimeOff.Models.Scheduling;

namespace Unified.TimeOff.Mappings;

internal static class SchedulingTimeOffEventMapper
{
    public static Event ToEvent(SchedulingTimeOffEntryRequest request, int? eventSeriesId)
    {
        var eventEntity = new Event
        {
            Title = request.Title.Trim(),
            EventTypeCode = TimeOffConstants.TimeOffEventTypeCode,
            StatusTypeCode = CalendarEventStatusTypeCodes.Draft,
        };

        ApplyToEvent(eventEntity, request, eventSeriesId);
        return eventEntity;
    }

    public static void ApplyToEvent(Event eventEntity, SchedulingTimeOffEntryRequest request, int? eventSeriesId)
    {
        eventEntity.EventSeriesId = eventSeriesId;
        eventEntity.Title = request.Title.Trim();
        eventEntity.Description = request.Description?.Trim();
        eventEntity.Notes = request.Notes?.Trim();
        eventEntity.Color = request.Color?.Trim();
        eventEntity.StartAtUtc = request.StartAtUtc;
        eventEntity.EndAtUtc = request.EndAtUtc;
        eventEntity.SeriesStartAtUtc = request.SeriesStartAtUtc ?? eventEntity.SeriesStartAtUtc;
        eventEntity.SeriesEndAtUtc = request.SeriesEndAtUtc ?? eventEntity.SeriesEndAtUtc;
        eventEntity.TimeZoneId = request.TimeZoneId?.Trim();
        eventEntity.AllDay = request.AllDay;
        eventEntity.SourceModule = TimeOffConstants.SourceModule;
        eventEntity.LocationId = request.LocationId;
    }

    public static EventSeries ToEventSeries(SchedulingTimeOffSeriesRequest request)
    {
        var eventSeries = new EventSeries
        {
            Title = request.Title.Trim(),
            EventTypeCode = TimeOffConstants.TimeOffEventTypeCode,
            StatusTypeCode = CalendarEventStatusTypeCodes.Draft,
        };

        ApplyToEventSeries(eventSeries, request);
        return eventSeries;
    }

    public static void ApplyToEventSeries(EventSeries eventSeries, SchedulingTimeOffSeriesRequest request)
    {
        eventSeries.Title = request.Title.Trim();
        eventSeries.Description = request.Description?.Trim();
        eventSeries.Notes = request.Notes?.Trim();
        eventSeries.Color = request.Color?.Trim();
        eventSeries.RecurrenceRule = request.RecurrenceRule;
        eventSeries.TimeZoneId = request.TimeZoneId?.Trim();
        eventSeries.StartAtUtc = request.StartAtUtc;
        eventSeries.EndAtUtc = request.EndAtUtc;
        eventSeries.AllDay = request.AllDay;
        eventSeries.LocationId = request.LocationId;
    }
}
