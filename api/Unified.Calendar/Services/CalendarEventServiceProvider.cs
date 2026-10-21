using Unified.Common.Calendar.Scheduling.Models;
using Unified.Db.Models.Calendar;

namespace Unified.Calendar.Services;

/// <summary>
/// Exposes whichever of the common <see cref="ICalendarEventService{TRequest,TEntity}"/> entry/series
/// instances are enabled for a calendar event type, so domain services can check
/// <see cref="SupportsEntry"/>/<see cref="SupportsSeries"/> and use the corresponding
/// <see cref="EntryService"/>/<see cref="SeriesService"/> instance directly, without needing to know up
/// front whether the event type supports one, the other, or both. Availability reflects registrations
/// for these domain types, not a global service inventory. Missing registrations mean unsupported
/// operations; errors constructing registered services propagate during resolution.
/// </summary>
public sealed class CalendarEventServiceProvider<TEntryRequest, TEntry, TSeriesRequest, TSeries>(
    ICalendarEventService<TEntryRequest, TEntry, Event>? entryService = null,
    ICalendarEventSeriesService<TSeriesRequest, TSeries>? seriesService = null
)
    where TEntryRequest : CalendarEventEntryRequest
    where TSeriesRequest : CalendarEventSeriesRequest
{
    public bool SupportsEntry => entryService is not null;

    public bool SupportsSeries => seriesService is not null;

    public ICalendarEventService<TEntryRequest, TEntry, Event> EntryService =>
        entryService ?? throw new NotSupportedException("Entry operations are not registered for this event type.");

    public ICalendarEventSeriesService<TSeriesRequest, TSeries> SeriesService =>
        seriesService ?? throw new NotSupportedException("Series operations are not registered for this event type.");
}
