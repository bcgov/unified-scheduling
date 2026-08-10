namespace Unified.Common.Helpers.Extensions;

public static class DateTimeOffsetExtensions
{
    public const string DateFormat = "yyyy-MM-dd";
    public const string TimeFormat = "HH:mm";

    public static bool IsValidDateFormat(string? dateString, string format = DateFormat)
    {
        if (string.IsNullOrEmpty(dateString))
        {
            return true;
        }

        return DateOnly.TryParseExact(dateString, format, null, System.Globalization.DateTimeStyles.None, out _);
    }

    /// <summary>
    /// Returns true when the string is a valid ISO 8601 datetime that includes
    /// an explicit time component (contains 'T') and a UTC offset or 'Z',
    /// e.g. "2026-01-10T08:30:00.000-08:00" or "2026-01-10T00:00:00Z".
    /// </summary>
    public static bool IsValidIsoDateTimeWithOffset(string? dateTimeString)
    {
        if (string.IsNullOrEmpty(dateTimeString))
        {
            return true;
        }

        return dateTimeString.Contains('T') && DateTimeOffset.TryParse(dateTimeString, out _);
    }
    /// <summary>
    /// Returns true when the string is a valid IANA or system timezone identifier
    /// (e.g. "America/Vancouver" or "Pacific Standard Time").
    /// </summary>
    public static bool IsValidIanaTimezone(string? timezoneId)
    {
        if (string.IsNullOrWhiteSpace(timezoneId))
        {
            return false;
        }

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

}
