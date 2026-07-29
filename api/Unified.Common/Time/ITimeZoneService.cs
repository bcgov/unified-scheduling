namespace Unified.Common.Time;

/// <summary>
/// Converts between UTC instants and local wall-clock values. Local values are rejected when they are invalid or
/// ambiguous in the selected time zone so callers must make any alternative policy explicit.
/// </summary>
public interface ITimeZoneService
{
    TimeZoneInfo ResolveRequired(string timeZoneId);

    DateTime ToLocalUnspecified(DateTimeOffset utcInstant, TimeZoneInfo timeZone);

    DateTimeOffset ToUtcInstant(DateTime localTime, TimeZoneInfo timeZone);

    UtcDateRange ConvertInclusiveLocalDateRangeToUtcRange(DateOnly startDate, DateOnly endDate, TimeZoneInfo timeZone);
}
