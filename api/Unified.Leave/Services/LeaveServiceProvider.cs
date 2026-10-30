using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Unified.Calendar;
using Unified.Calendar.Services;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Leave;
using Unified.Leave.Factories;
using Unified.Leave.Models.Scheduling;

namespace Unified.Leave.Services;

/// <summary>
/// Exposes the Leave domain's registered entry and series operations through the shared calendar provider.
/// Register configures both pipelines with LeaveEventFactory; CalendarModule supplies the generic provider.
/// </summary>
public sealed class LeaveServiceProvider(
    CalendarEventServiceProvider<
        NormalizedLeaveEntryRequest,
        LeaveEntry,
        NormalizedLeaveSeriesRequest,
        LeaveSeries
    > calendarServices
)
{
    public bool SupportsEntry => calendarServices.SupportsEntry;

    public bool SupportsSeries => calendarServices.SupportsSeries;

    public ICalendarEventService<NormalizedLeaveEntryRequest, LeaveEntry, Event> EntryService =>
        calendarServices.EntryService;

    public ICalendarEventSeriesService<NormalizedLeaveSeriesRequest, LeaveSeries> SeriesService =>
        calendarServices.SeriesService;

    public static IServiceCollection Register(IServiceCollection services)
    {
        services.AddCalendarEventEntry<NormalizedLeaveEntryRequest, LeaveEntry, LeaveEventFactory>();
        services.AddCalendarEventSeries<NormalizedLeaveSeriesRequest, LeaveSeries, LeaveEventFactory>();
        services.TryAddScoped<LeaveServiceProvider>();

        return services;
    }
}
