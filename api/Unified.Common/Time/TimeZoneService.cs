namespace Unified.Common.Time;

public sealed class TimeZoneService : ITimeZoneService
{
    private const string DateFormat = "yyyy-MM-dd";

    public static string NormalizeTimeZoneId(string timeZoneId)
    {
        var normalized = timeZoneId.Trim();
        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(normalized, out var ianaTimeZoneId))
            normalized = ianaTimeZoneId;

        return TimeZoneInfo.FindSystemTimeZoneById(normalized).Id;
    }

    public static string? NormalizeOptionalTimeZoneId(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return null;

        return TryNormalizeTimeZoneId(timeZoneId, out var normalizedTimeZoneId) ? normalizedTimeZoneId : null;
    }

    public static bool IsValidTimeZoneId(string? timeZoneId) =>
        string.IsNullOrWhiteSpace(timeZoneId) || TryNormalizeTimeZoneId(timeZoneId, out _);

    public TimeZoneInfo ResolveOrUtc(string? timeZoneId)
    {
        var normalizedTimeZoneId = NormalizeOptionalTimeZoneId(timeZoneId);
        return normalizedTimeZoneId is null
            ? TimeZoneInfo.Utc
            : FindTimeZone(normalizedTimeZoneId);
    }

    public TimeZoneInfo ResolveRequired(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            throw new ArgumentException("A time zone ID is required.", nameof(timeZoneId));

        return FindTimeZone(NormalizeTimeZoneId(timeZoneId));
    }

    private static TimeZoneInfo FindTimeZone(string timeZoneId)
    {
        // Windows can have the BC rules installed while ICU still maps Vancouver to US Pacific rules.
        // Keep the IANA ID and use the installed BC rules, including historical DST transitions.
        if (
            OperatingSystem.IsWindows()
            && string.Equals(timeZoneId, "America/Vancouver", StringComparison.OrdinalIgnoreCase)
            && TimeZoneInfo.TryFindSystemTimeZoneById("British Columbia Standard Time", out var timeZone)
        )
        {
            return TimeZoneInfo.CreateCustomTimeZone(
                timeZoneId,
                timeZone.BaseUtcOffset,
                timeZone.DisplayName,
                timeZone.StandardName,
                timeZone.DaylightName,
                timeZone.GetAdjustmentRules()
            );
        }

        return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
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

        // A changed base offset can leave historical Unix rules with zero DaylightDelta,
        // which .NET's IsInvalidTime/IsAmbiguousTime checks ignore. Validate candidates
        // against UTC-to-local conversion instead, using only offsets from the zone's rules.
        var offsets = timeZone
            .GetAdjustmentRules()
            .SelectMany(rule =>
                new[]
                {
                    timeZone.BaseUtcOffset + rule.BaseUtcOffsetDelta,
                    timeZone.BaseUtcOffset + rule.BaseUtcOffsetDelta + rule.DaylightDelta,
                }
            )
            .Append(timeZone.BaseUtcOffset)
            .Distinct();
        var candidates = offsets
            .Select(offset => unspecifiedLocalTime.Ticks - offset.Ticks)
            .Where(ticks => ticks >= DateTime.MinValue.Ticks && ticks <= DateTime.MaxValue.Ticks)
            .Select(ticks => new DateTimeOffset(ticks, TimeSpan.Zero))
            .Where(instant => ToLocalUnspecified(instant, timeZone) == unspecifiedLocalTime)
            .Take(2)
            .ToArray();

        if (candidates.Length == 0)
            throw new InvalidOperationException(
                $"Local time {unspecifiedLocalTime:O} is invalid in time zone {timeZone.Id}."
            );

        if (candidates.Length > 1)
            throw new InvalidOperationException(
                $"Local time {unspecifiedLocalTime:O} is ambiguous in time zone {timeZone.Id}."
            );

        return candidates[0];
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

    private static bool TryNormalizeTimeZoneId(string timeZoneId, out string? normalizedTimeZoneId)
    {
        try
        {
            normalizedTimeZoneId = NormalizeTimeZoneId(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            normalizedTimeZoneId = null;
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            normalizedTimeZoneId = null;
            return false;
        }
    }
}
