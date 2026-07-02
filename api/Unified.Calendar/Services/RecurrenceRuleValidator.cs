using Unified.Db.Models.Calendar;

namespace Unified.Calendar.Services;

public sealed class IcalNetRecurrenceRuleValidator(
    IRecurrenceExpander recurrenceExpander,
    ICalendarDateTimeService calendarDateTimeService
) : IRecurrenceRuleValidator
{
    public RecurrenceValidationResult Validate(
        string recurrenceRule,
        DateTimeOffset seriesStartAtUtc,
        DateTimeOffset? seriesEndAtUtc,
        string? timeZoneId,
        RecurrenceValidationOptions options
    )
    {
        var errors = new List<string>();
        Ical.Net.DataTypes.RecurrenceRule rule;
        var validationSeries = new EventSeries
        {
            RecurrenceRule = recurrenceRule,
            StartAtUtc = seriesStartAtUtc,
            EndAtUtc = seriesEndAtUtc,
            TimeZoneId = timeZoneId,
        };

        try
        {
            var timeZone = calendarDateTimeService.ResolveTimeZone(timeZoneId);
            var localRange = calendarDateTimeService.GetLocalTimeRange(validationSeries, timeZone);
            rule = IcalNetRecurrenceEventFactory
                .Create(
                    recurrenceRule,
                    localRange,
                    string.IsNullOrWhiteSpace(timeZoneId) ? timeZone.Id : timeZoneId.Trim()
                )
                .RecurrenceRule!;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException)
        {
            return RecurrenceValidationResult.Failure($"RRULE is invalid: {ex.Message}");
        }

        if (options.RequireBoundedRule && rule.Count is null && rule.Until is null)
            errors.Add("RRULE must be bounded by COUNT or UNTIL.");

        if (rule.Count.HasValue && rule.Count.Value > options.MaximumOccurrences)
            errors.Add("RRULE generates too many occurrences.");

        var latestAllowedStartInclusive = seriesStartAtUtc.Add(options.MaximumDuration);
        var untilUtc = rule.Until is null ? (DateTimeOffset?)null : new DateTimeOffset(rule.Until.AsUtc);
        if (untilUtc.HasValue && untilUtc.Value > latestAllowedStartInclusive)
            errors.Add("RRULE duration exceeds the maximum allowed duration.");

        var shouldCountOccurrences =
            !errors.Contains("RRULE must be bounded by COUNT or UNTIL.")
            && !errors.Contains("RRULE generates too many occurrences.")
            && !errors.Contains("RRULE duration exceeds the maximum allowed duration.");

        if (shouldCountOccurrences)
        {
            RecurrenceCountResult countResult;
            try
            {
                var validationRangeEndExclusive = ToExclusiveEndIncluding(latestAllowedStartInclusive);
                countResult = recurrenceExpander.CountWithin(
                    validationSeries,
                    seriesStartAtUtc,
                    validationRangeEndExclusive,
                    stopAfter: options.MaximumOccurrences + 1
                );
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException)
            {
                return RecurrenceValidationResult.Failure($"RRULE is invalid: {ex.Message}");
            }

            if (countResult.Count == 0)
                errors.Add("RRULE must generate at least one occurrence.");

            if (countResult.Count > options.MaximumOccurrences)
                errors.Add("RRULE generates too many occurrences.");

            if (rule.Count.HasValue && countResult.Count < rule.Count.Value && !untilUtc.HasValue)
                errors.Add("RRULE duration exceeds the maximum allowed duration.");
        }

        return errors.Count == 0
            ? RecurrenceValidationResult.Success
            : new RecurrenceValidationResult { Errors = errors };
    }

    private static DateTimeOffset ToExclusiveEndIncluding(DateTimeOffset inclusiveInstant) =>
        inclusiveInstant.AddTicks(1);
}
