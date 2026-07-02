using Unified.Db.Models.Calendar;

namespace Unified.Calendar.Services;

public interface IRecurrenceExpander
{
    IReadOnlyCollection<SeriesEntry> ExpandWithin(
        EventSeries eventSeries,
        DateTimeOffset rangeStartAtUtc,
        DateTimeOffset rangeEndAtUtc
    );

    IReadOnlyCollection<SeriesEntry> ExpandAllBounded(EventSeries eventSeries, int maximumOccurrences);

    RecurrenceCountResult CountWithin(
        EventSeries eventSeries,
        DateTimeOffset rangeStartAtUtc,
        DateTimeOffset rangeEndAtUtc,
        int stopAfter
    );
}
