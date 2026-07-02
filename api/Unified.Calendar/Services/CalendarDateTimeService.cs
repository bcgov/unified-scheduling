using Microsoft.Extensions.Options;
using Unified.Calendar.Options;
using Unified.Db.Models.Calendar;

namespace Unified.Calendar.Services;

public sealed class CalendarDateTimeService(IOptions<CalendarDateTimeOptions> options) : ICalendarDateTimeService
{
    private readonly CalendarDateTimeOptions _options = options.Value;

    public TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        return ResolveTimeZone(timeZoneId, fallbackTimeZoneId: null);
    }

    public TimeZoneInfo ResolveTimeZone(string? timeZoneId, string? fallbackTimeZoneId)
    {
        var resolvedTimeZoneId = string.IsNullOrWhiteSpace(timeZoneId) ? fallbackTimeZoneId?.Trim() : timeZoneId.Trim();

        if (string.IsNullOrWhiteSpace(resolvedTimeZoneId))
            resolvedTimeZoneId = _options.DefaultTimeZoneId.Trim();

        return TimeZoneInfo.FindSystemTimeZoneById(resolvedTimeZoneId);
    }

    public DateTime ToLocalTime(DateTimeOffset utcInstant, TimeZoneInfo timeZone)
    {
        var localInstant = TimeZoneInfo.ConvertTime(utcInstant, timeZone);
        return DateTime.SpecifyKind(localInstant.DateTime, DateTimeKind.Unspecified);
    }

    public DateTimeOffset ToUtcInstant(DateTime localTime, TimeZoneInfo timeZone)
    {
        var unspecifiedLocalTime = DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified);

        if (timeZone.IsInvalidTime(unspecifiedLocalTime))
        {
            throw new InvalidOperationException(
                $"Local time {unspecifiedLocalTime:O} is invalid in time zone {timeZone.Id}."
            );
        }

        if (timeZone.IsAmbiguousTime(unspecifiedLocalTime))
        {
            throw new InvalidOperationException(
                $"Local time {unspecifiedLocalTime:O} is ambiguous in time zone {timeZone.Id}."
            );
        }

        var utcDateTime = TimeZoneInfo.ConvertTimeToUtc(unspecifiedLocalTime, timeZone);
        return new DateTimeOffset(utcDateTime, TimeSpan.Zero);
    }

    /// <summary>
    /// Convert a user-facing inclusive date range (ie. I want all events from Jan 1 - Jan 2), to an API appropriate format.
    /// Note, this is because it is easier to handle < Jan 3 00:00:00 then it is to handle < Jan 2 23:59:59:999999... due to precision issues.
    /// </summary>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <param name="timeZone"></param>
    /// <returns></returns>
    public UtcDateRange ConvertInclusiveLocalDateRangeToUtcRange(
        DateOnly startDate,
        DateOnly endDate,
        TimeZoneInfo timeZone
    )
    {
        DateOnly exclusiveEndDate = endDate.AddDays(1);
        return new UtcDateRange(
            ToUtcInstant(startDate.ToDateTime(TimeOnly.MinValue), timeZone),
            ToUtcInstant(exclusiveEndDate.ToDateTime(TimeOnly.MinValue), timeZone)
        );
    }

    public EventSeriesLocalTimeRange GetLocalTimeRange(EventSeries eventSeries, TimeZoneInfo timeZone)
    {
        var startLocal = ToLocalTime(eventSeries.StartAtUtc, timeZone);
        var endLocal = eventSeries.EndAtUtc.HasValue
            ? ToLocalTime(eventSeries.EndAtUtc.Value, timeZone)
            : (DateTime?)null;

        return new EventSeriesLocalTimeRange(startLocal, endLocal, endLocal - startLocal);
    }
}
