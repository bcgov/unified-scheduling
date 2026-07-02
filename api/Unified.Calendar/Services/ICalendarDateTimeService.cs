using Unified.Db.Models.Calendar;

namespace Unified.Calendar.Services;

public interface ICalendarDateTimeService
{
    TimeZoneInfo ResolveTimeZone(string? timeZoneId);

    TimeZoneInfo ResolveTimeZone(string? timeZoneId, string? fallbackTimeZoneId);

    DateTime ToLocalTime(DateTimeOffset utcInstant, TimeZoneInfo timeZone);

    DateTimeOffset ToUtcInstant(DateTime localTime, TimeZoneInfo timeZone);

    UtcDateRange ConvertInclusiveLocalDateRangeToUtcRange(DateOnly startDate, DateOnly endDate, TimeZoneInfo timeZone);

    EventSeriesLocalTimeRange GetLocalTimeRange(EventSeries eventSeries, TimeZoneInfo timeZone);
}
