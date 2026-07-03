using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;

namespace Unified.Calendar.Services;

internal static class IcalNetRecurrenceEventFactory
{
    public static CalendarEvent Create(string recurrenceRule, EventSeriesLocalTimeRange localRange, string timeZoneId)
    {
        var normalizedRecurrenceRule = NormalizeRecurrenceRule(recurrenceRule);

        return new CalendarEvent
        {
            DtStart = ToCalDateTime(localRange.StartLocal, timeZoneId),
            DtEnd = localRange.EndLocal.HasValue ? ToCalDateTime(localRange.EndLocal.Value, timeZoneId) : null,
            RecurrenceRule = new RecurrenceRule(normalizedRecurrenceRule),
        };
    }

    private static string NormalizeRecurrenceRule(string recurrenceRule)
    {
        if (string.IsNullOrWhiteSpace(recurrenceRule))
            throw new FormatException("RRULE is required.");

        var normalizedRecurrenceRule = recurrenceRule.Trim();

        if (normalizedRecurrenceRule.StartsWith("RRULE:", StringComparison.OrdinalIgnoreCase))
            normalizedRecurrenceRule = normalizedRecurrenceRule["RRULE:".Length..].Trim();

        if (string.IsNullOrWhiteSpace(normalizedRecurrenceRule))
            throw new FormatException("RRULE is required.");

        return normalizedRecurrenceRule;
    }

    private static CalDateTime ToCalDateTime(DateTime value, string timeZoneId) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Unspecified), timeZoneId);
}
