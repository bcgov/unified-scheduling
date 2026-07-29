using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Unified.Calendar.Extensions;
using Unified.Common.Time;
using Unified.Db.Models.Calendar;

namespace Unified.Calendar.Services;

public sealed class IcalNetRecurrenceExpander(
    ITimeZoneService timeZoneService,
    ICalendarTimeZoneResolver timeZoneResolver
) : IRecurrenceExpander
{
    private static readonly TimeSpan DstOverlapBuffer = TimeSpan.FromHours(1);

    public IReadOnlyCollection<SeriesEntry> ExpandWithin(
        EventSeries eventSeries,
        DateTimeOffset rangeStartAtUtc,
        DateTimeOffset rangeEndAtUtc
    ) => EnumerateWithin(eventSeries, rangeStartAtUtc, rangeEndAtUtc).ToList();

    public IReadOnlyCollection<SeriesEntry> ExpandAllBounded(EventSeries eventSeries, int maximumOccurrences)
    {
        if (maximumOccurrences <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumOccurrences));

        var expansionContext = CreateExpansionContext(eventSeries);
        var recurrenceRule = expansionContext.CalendarEvent?.RecurrenceRule;
        if (recurrenceRule is null)
            throw new InvalidOperationException("RRULE is required for bounded expansion.");

        if (recurrenceRule.Count is null && recurrenceRule.Until is null)
            throw new InvalidOperationException("Cannot expand all occurrences for an unbounded RRULE.");

        var localRangeStart = timeZoneService.ToLocalUnspecified(eventSeries.StartAtUtc, expansionContext.TimeZone);
        var untilUtc = recurrenceRule.Until is null
            ? (DateTimeOffset?)null
            : new DateTimeOffset(recurrenceRule.Until.AsUtc);
        var occurrences = ExpandOccurrences(expansionContext, localRangeStart)
            .TakeWhile(occurrence => !untilUtc.HasValue || occurrence.StartAtUtc <= untilUtc.Value);
        var results = new List<SeriesEntry>();
        foreach (var occurrence in occurrences)
        {
            results.Add(occurrence);

            if (results.Count > maximumOccurrences)
                throw new InvalidOperationException("RRULE generates too many occurrences.");
        }

        return results;
    }

    public int CountWithin(
        EventSeries eventSeries,
        DateTimeOffset rangeStartAtUtc,
        DateTimeOffset rangeEndAtUtc,
        int stopAfter
    )
    {
        if (stopAfter <= 0)
            throw new ArgumentOutOfRangeException(nameof(stopAfter));

        var count = 0;
        foreach (var occurrence in EnumerateWithin(eventSeries, rangeStartAtUtc, rangeEndAtUtc))
        {
            count++;

            if (count >= stopAfter)
            {
                return count;
            }
        }

        return count;
    }

    private ExpansionContext CreateExpansionContext(EventSeries eventSeries)
    {
        var timeZone = timeZoneResolver.Resolve(eventSeries.TimeZoneId);
        var timeZoneId = string.IsNullOrWhiteSpace(eventSeries.TimeZoneId)
            ? timeZone.Id
            : eventSeries.TimeZoneId.Trim();
        var localRange = eventSeries.ToLocalTimeRange(timeZoneService, timeZone);

        if (string.IsNullOrWhiteSpace(eventSeries.RecurrenceRule))
            return new ExpansionContext(timeZone, timeZoneId, localRange, CalendarEvent: null);

        var calendarEvent = IcalNetRecurrenceEventFactory.Create(eventSeries.RecurrenceRule, localRange, timeZoneId);
        return new ExpansionContext(timeZone, timeZoneId, localRange, calendarEvent);
    }

    private IEnumerable<SeriesEntry> ExpandOccurrences(ExpansionContext expansionContext, DateTime localRangeStart)
    {
        // iCal.NET 5.2.3 does not expose a public evaluator limit in the APIs used here.
        // Callers bound enumeration with CountWithin stopAfter, MaximumDuration, and ExpandAllBounded caps.
        return expansionContext
            .CalendarEvent!.GetOccurrences(
                new CalDateTime(
                    DateTime.SpecifyKind(localRangeStart, DateTimeKind.Unspecified),
                    expansionContext.TimeZoneId
                )
            )
            .Select(occurrence =>
            {
                var occurrenceLocalStart = DateTime.SpecifyKind(
                    occurrence.Period.StartTime.Value,
                    DateTimeKind.Unspecified
                );
                var occurrenceStartAtUtc = timeZoneService.ToUtcInstant(occurrenceLocalStart, expansionContext.TimeZone);
                DateTimeOffset? occurrenceEndAtUtc = null;
                if (expansionContext.LocalRange.Duration.HasValue)
                {
                    var occurrenceLocalEnd = occurrenceLocalStart + expansionContext.LocalRange.Duration.Value;
                    occurrenceEndAtUtc = timeZoneService.ToUtcInstant(occurrenceLocalEnd, expansionContext.TimeZone);
                }

                return new SeriesEntry { StartAtUtc = occurrenceStartAtUtc, EndAtUtc = occurrenceEndAtUtc };
            });
    }

    private IEnumerable<SeriesEntry> EnumerateWithin(
        EventSeries eventSeries,
        DateTimeOffset rangeStartAtUtc,
        DateTimeOffset rangeEndAtUtc
    )
    {
        var expansionContext = CreateExpansionContext(eventSeries);
        if (expansionContext.CalendarEvent is null)
        {
            if (IsInRange(eventSeries.StartAtUtc, eventSeries.EndAtUtc, rangeStartAtUtc, rangeEndAtUtc))
                yield return new SeriesEntry { StartAtUtc = eventSeries.StartAtUtc, EndAtUtc = eventSeries.EndAtUtc };

            yield break;
        }

        var eventDuration = eventSeries.EndAtUtc - eventSeries.StartAtUtc;
        var enumerationStartAtUtc =
            eventDuration.HasValue && eventDuration.Value > TimeSpan.Zero
                ? rangeStartAtUtc - eventDuration.Value - DstOverlapBuffer
                : eventSeries.StartAtUtc;
        var localRangeStart = timeZoneService.ToLocalUnspecified(enumerationStartAtUtc, expansionContext.TimeZone);
        foreach (var occurrence in ExpandOccurrences(expansionContext, localRangeStart))
        {
            if (occurrence.StartAtUtc >= rangeEndAtUtc)
                yield break;

            if (IsInRange(occurrence.StartAtUtc, occurrence.EndAtUtc, rangeStartAtUtc, rangeEndAtUtc))
                yield return occurrence;
        }
    }

    private static bool IsInRange(
        DateTimeOffset startAtUtc,
        DateTimeOffset? endAtUtc,
        DateTimeOffset rangeStartAtUtc,
        DateTimeOffset rangeEndAtUtc
    )
    {
        return startAtUtc < rangeEndAtUtc
            && (
                endAtUtc == null
                    ? startAtUtc >= rangeStartAtUtc && startAtUtc < rangeEndAtUtc
                    : endAtUtc > rangeStartAtUtc
            );
    }

    private sealed record ExpansionContext(
        TimeZoneInfo TimeZone,
        string TimeZoneId,
        EventSeriesLocalTimeRange LocalRange,
        CalendarEvent? CalendarEvent
    );
}
