using Unified.Calendar.Services;
using Unified.Common.Time;
using Unified.Db.Models.Calendar;

namespace Unified.Calendar.Extensions;

public static class EventSeriesExtensions
{
    public static EventSeriesLocalTimeRange ToLocalTimeRange(
        this EventSeries eventSeries,
        ITimeZoneService timeZoneService,
        TimeZoneInfo timeZone
    )
    {
        var startLocal = timeZoneService.ToLocalUnspecified(eventSeries.StartAtUtc, timeZone);
        var endLocal = eventSeries.EndAtUtc.HasValue
            ? timeZoneService.ToLocalUnspecified(eventSeries.EndAtUtc.Value, timeZone)
            : (DateTime?)null;

        return new EventSeriesLocalTimeRange(startLocal, endLocal, endLocal - startLocal);
    }
}
