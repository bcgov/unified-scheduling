namespace Unified.Common.Time;

public sealed class TimeZoneService : ITimeZoneService
{
    public static bool IsValidTimeZoneId(string? timeZoneId) =>
        string.IsNullOrWhiteSpace(timeZoneId) || TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId.Trim(), out _);

    public TimeZoneInfo ResolveRequired(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            throw new ArgumentException("A time zone ID is required.", nameof(timeZoneId));

        return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
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
        var exclusiveEndDate = endDate.AddDays(1);
        return new UtcDateRange(
            ToUtcInstant(startDate.ToDateTime(TimeOnly.MinValue), timeZone),
            ToUtcInstant(exclusiveEndDate.ToDateTime(TimeOnly.MinValue), timeZone)
        );
    }
}
