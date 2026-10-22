using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Unified.Calendar;
using Unified.Calendar.Services;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Factories;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public sealed class SchedulingServiceProvider(
    CalendarEventServiceProvider<ShiftEntryRequest, ShiftEntry, ShiftSeriesRequest, ShiftSeries> shiftServices,
    CalendarEventServiceProvider<
        AssignmentEntryRequest,
        AssignmentEntry,
        AssignmentSeriesRequest,
        AssignmentSeries
    > assignmentServices
)
{
    public ICalendarEventService<ShiftEntryRequest, ShiftEntry, Event> ShiftEntryService => shiftServices.EntryService;

    public ICalendarEventSeriesService<ShiftSeriesRequest, ShiftSeries> ShiftSeriesService =>
        shiftServices.SeriesService;

    public ICalendarEventService<AssignmentEntryRequest, AssignmentEntry, Event> AssignmentEntryService =>
        assignmentServices.EntryService;

    public ICalendarEventSeriesService<AssignmentSeriesRequest, AssignmentSeries> AssignmentSeriesService =>
        assignmentServices.SeriesService;

    public static IServiceCollection Register(IServiceCollection services)
    {
        services.AddCalendarEventEntry<ShiftEntryRequest, ShiftEntry, ShiftEventFactory>();
        services.AddCalendarEventSeries<ShiftSeriesRequest, ShiftSeries, ShiftEventFactory>();
        services.AddCalendarEventEntry<AssignmentEntryRequest, AssignmentEntry, AssignmentEventFactory>();
        services.AddCalendarEventSeries<AssignmentSeriesRequest, AssignmentSeries, AssignmentEventFactory>();
        services.TryAddScoped<SchedulingServiceProvider>();

        return services;
    }
}
