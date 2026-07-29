using Unified.Calendar.Extensions;
using Unified.Common.Time;
using Unified.Db.Models.Calendar;

namespace Unified.Calendar.Services;

public sealed class IcalNetRecurrenceRuleValidator(
    IRecurrenceExpander recurrenceExpander,
    ITimeZoneService timeZoneService,
    ICalendarTimeZoneResolver timeZoneResolver
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
            var timeZone = timeZoneResolver.Resolve(timeZoneId);
            var localRange = validationSeries.ToLocalTimeRange(timeZoneService, timeZone);
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

        var isBounded = rule.Count is not null || rule.Until is not null;
        var isCountWithinLimit = !rule.Count.HasValue || rule.Count.Value <= options.MaximumOccurrences;
        if (options.RequireBoundedRule && !isBounded)
            errors.Add("RRULE must be bounded by COUNT or UNTIL.");

        if (!isCountWithinLimit)
            errors.Add("RRULE generates too many occurrences.");

        var latestAllowedStartInclusive = seriesStartAtUtc.Add(options.MaximumDuration);
        var untilUtc = rule.Until is null ? (DateTimeOffset?)null : new DateTimeOffset(rule.Until.AsUtc);
        var isDurationWithinLimit = !untilUtc.HasValue || untilUtc.Value <= latestAllowedStartInclusive;
        if (!isDurationWithinLimit)
            errors.Add("RRULE duration exceeds the maximum allowed duration.");

        var shouldCountOccurrences = isBounded && isCountWithinLimit && isDurationWithinLimit;

        if (shouldCountOccurrences)
        {
            int occurrenceCount;
            try
            {
                var validationRangeEndExclusive = ToExclusiveEndIncluding(latestAllowedStartInclusive);
                occurrenceCount = recurrenceExpander.CountWithin(
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

            if (occurrenceCount == 0)
                errors.Add("RRULE must generate at least one occurrence.");

            if (occurrenceCount > options.MaximumOccurrences)
                errors.Add("RRULE generates too many occurrences.");

            if (rule.Count.HasValue && occurrenceCount < rule.Count.Value && !untilUtc.HasValue)
                errors.Add("RRULE duration exceeds the maximum allowed duration.");
        }

        return errors.Count == 0
            ? RecurrenceValidationResult.Success
            : new RecurrenceValidationResult { Errors = errors };
    }

    private static DateTimeOffset ToExclusiveEndIncluding(DateTimeOffset inclusiveInstant) =>
        inclusiveInstant.AddTicks(1);
}
