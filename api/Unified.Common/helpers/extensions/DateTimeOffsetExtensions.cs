namespace Unified.Common.Helpers.Extensions;

public static class DateTimeOffsetExtensions
{
    public const string DateFormat = "yyyy-MM-dd";

    public static bool IsValidDateFormat(string? dateString, string format = DateFormat)
    {
        if (string.IsNullOrEmpty(dateString))
        {
            return true;
        }

        return DateOnly.TryParseExact(dateString, format, null, System.Globalization.DateTimeStyles.None, out _);
    }

    /// <summary>
    /// Converts a date string (yyyy-MM-dd) to DateTimeOffset at start-of-day in the given timezone.
    /// </summary>
    public static DateTimeOffset FromDateStringToStartOfDayInTimeZone(string dateString, string? timezoneId)
    {
        if (
            !DateTime.TryParseExact(
                dateString,
                DateFormat,
                null,
                System.Globalization.DateTimeStyles.None,
                out var date
            )
        )
        {
            throw new ArgumentException($"Invalid date format. Expected {DateFormat}, got {dateString}");
        }

        var timezone = ResolveTimeZone(timezoneId);
        var localDate = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var offset = timezone.GetUtcOffset(localDate);
        return new DateTimeOffset(localDate, offset).ToUniversalTime();
    }

    /// <summary>
    /// Converts a date string (yyyy-MM-dd) to DateTimeOffset at end-of-day in the given timezone.
    /// </summary>
    public static DateTimeOffset FromDateStringToEndOfDayInTimeZone(string dateString, string? timezoneId)
    {
        if (
            !DateTime.TryParseExact(
                dateString,
                DateFormat,
                null,
                System.Globalization.DateTimeStyles.None,
                out var date
            )
        )
        {
            throw new ArgumentException($"Invalid date format. Expected {DateFormat}, got {dateString}");
        }

        var timezone = ResolveTimeZone(timezoneId);
        var localDate = new DateTime(date.Year, date.Month, date.Day, 23, 59, 59, 999, DateTimeKind.Unspecified);
        var offset = timezone.GetUtcOffset(localDate);
        return new DateTimeOffset(localDate, offset).ToUniversalTime();
    }

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

    public static DateTimeOffset ToTimeZone(this DateTimeOffset value, string? timezoneId)
    {
        var timezone = ResolveTimeZone(timezoneId);
        return TimeZoneInfo.ConvertTime(value, timezone);
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
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
