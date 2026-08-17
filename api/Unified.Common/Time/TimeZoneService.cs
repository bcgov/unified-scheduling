namespace Unified.Common.Time;

public sealed class TimeZoneService : ITimeZoneService
{
    private const string DateFormat = "yyyy-MM-dd";

    public static bool IsValidTimeZoneId(string? timeZoneId) =>
        string.IsNullOrWhiteSpace(timeZoneId) || TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId.Trim(), out _);

    public TimeZoneInfo ResolveOrUtc(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return TimeZoneInfo.Utc;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
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

    public TimeZoneInfo ResolveRequired(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            throw new ArgumentException("A time zone ID is required.", nameof(timeZoneId));

        return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
    }

    public DateTimeOffset ToTimeZone(DateTimeOffset value, string? timeZoneId) =>
        TimeZoneInfo.ConvertTime(value, ResolveOrUtc(timeZoneId));

    public DateTimeOffset FromDateStringToStartOfDayInTimeZone(string dateString, string? timeZoneId)
    {
        var date = ParseDate(dateString);
        var timeZone = ResolveOrUtc(timeZoneId);
        var localDate = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var offset = timeZone.GetUtcOffset(localDate);
        return new DateTimeOffset(localDate, offset).ToUniversalTime();
    }

    public DateTimeOffset FromDateStringToEndOfDayInTimeZone(string dateString, string? timeZoneId)
    {
        var date = ParseDate(dateString);
        var timeZone = ResolveOrUtc(timeZoneId);
        var localDate = new DateTime(date.Year, date.Month, date.Day, 23, 59, 59, 999, DateTimeKind.Unspecified);
        var offset = timeZone.GetUtcOffset(localDate);
        return new DateTimeOffset(localDate, offset).ToUniversalTime();
    }

    public DateTime ToLocalUnspecified(DateTimeOffset utcInstant, TimeZoneInfo timeZone)
    {
        var localInstant = TimeZoneInfo.ConvertTime(utcInstant, timeZone);
        return DateTime.SpecifyKind(localInstant.DateTime, DateTimeKind.Unspecified);
    }

    public DateTimeOffset ToUtcInstant(DateTime localTime, TimeZoneInfo timeZone)
    {
        var unspecifiedLocalTime = DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified);

        if (timeZone.IsInvalidTime(unspecifiedLocalTime))
            throw new InvalidOperationException(
                $"Local time {unspecifiedLocalTime:O} is invalid in time zone {timeZone.Id}."
            );

        if (timeZone.IsAmbiguousTime(unspecifiedLocalTime))
            throw new InvalidOperationException(
                $"Local time {unspecifiedLocalTime:O} is ambiguous in time zone {timeZone.Id}."
            );

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(unspecifiedLocalTime, timeZone), TimeSpan.Zero);
    }

    public UtcDateRange ConvertInclusiveLocalDateRangeToUtcRange(
        DateOnly startDate,
        DateOnly endDate,
        TimeZoneInfo timeZone
    )
    {
        if (endDate < startDate)
            throw new ArgumentException("End date must be on or after start date.", nameof(endDate));

        if (startDate > TimeZoneDateRangeLimits.MaximumSupportedDate)
            throw new ArgumentOutOfRangeException(
                nameof(startDate),
                startDate,
                $"Start date cannot be after {TimeZoneDateRangeLimits.MaximumSupportedDate:yyyy-MM-dd}."
            );

        if (endDate > TimeZoneDateRangeLimits.MaximumSupportedDate)
            throw new ArgumentOutOfRangeException(
                nameof(endDate),
                endDate,
                $"End date cannot be after {TimeZoneDateRangeLimits.MaximumSupportedDate:yyyy-MM-dd}."
            );
        var exclusiveEndDate = endDate.AddDays(1);
        return new UtcDateRange(
            ToUtcInstant(startDate.ToDateTime(TimeOnly.MinValue), timeZone),
            ToUtcInstant(exclusiveEndDate.ToDateTime(TimeOnly.MinValue), timeZone)
        );
    }

    private static DateTime ParseDate(string dateString)
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

        return date;
    }
}
