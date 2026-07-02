using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;

namespace Unified.Calendar.Services;

internal static class IcalNetRecurrenceEventFactory
{
    public static CalendarEvent Create(string recurrenceRule, EventSeriesLocalTimeRange localRange, string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(recurrenceRule))
            throw new FormatException("RRULE is required.");

        return new CalendarEvent
        {
            DtStart = ToCalDateTime(localRange.StartLocal, timeZoneId),
            DtEnd = localRange.EndLocal.HasValue ? ToCalDateTime(localRange.EndLocal.Value, timeZoneId) : null,
            RecurrenceRule = new RecurrenceRule(recurrenceRule),
        };
    }

    private static CalDateTime ToCalDateTime(DateTime value, string timeZoneId) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Unspecified), timeZoneId);
}
