namespace Unified.Common.Helpers.Extensions;

public static class DateTimeOffsetExtensions
{
    public static DateTimeOffset ToStartOfDayUtcInTimeZone(this DateTimeOffset value, string? timezoneId)
    {
        var timezone = ResolveTimeZone(timezoneId);
        var localDate = new DateTime(value.Year, value.Month, value.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var offset = timezone.GetUtcOffset(localDate);
        return new DateTimeOffset(localDate, offset).ToUniversalTime();
    }

    public static DateTimeOffset ToEndOfDayUtcInTimeZone(this DateTimeOffset value, string? timezoneId)
    {
        var timezone = ResolveTimeZone(timezoneId);
        var localDate = new DateTime(value.Year, value.Month, value.Day, 23, 59, 59, 999, DateTimeKind.Unspecified);
        var offset = timezone.GetUtcOffset(localDate);
        return new DateTimeOffset(localDate, offset).ToUniversalTime();
    }

    public static TimeZoneInfo ResolveTimeZone(string? timezoneId)
    {
        if (string.IsNullOrWhiteSpace(timezoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            if (timezoneId == "America/Vancouver")
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
            }

            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
