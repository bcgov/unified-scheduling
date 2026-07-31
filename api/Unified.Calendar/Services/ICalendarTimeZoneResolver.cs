namespace Unified.Calendar.Services;

public interface ICalendarTimeZoneResolver
{
    TimeZoneInfo Resolve(string? requestedTimeZoneId, string? fallbackTimeZoneId = null);
}
