using Microsoft.Extensions.Options;
using Unified.Calendar.Options;
using Unified.Common.Time;

namespace Unified.Calendar.Services;

public sealed class CalendarTimeZoneResolver(
    IOptions<CalendarDateTimeOptions> options,
    ITimeZoneService timeZoneService
) : ICalendarTimeZoneResolver
{
    private readonly CalendarDateTimeOptions options = options.Value;

    public TimeZoneInfo Resolve(string? requestedTimeZoneId, string? fallbackTimeZoneId = null)
    {
        var timeZoneId = string.IsNullOrWhiteSpace(requestedTimeZoneId)
            ? fallbackTimeZoneId?.Trim()
            : requestedTimeZoneId.Trim();

        if (string.IsNullOrWhiteSpace(timeZoneId))
            timeZoneId = options.DefaultTimeZoneId.Trim();

        return timeZoneService.ResolveRequired(timeZoneId);
    }
}
