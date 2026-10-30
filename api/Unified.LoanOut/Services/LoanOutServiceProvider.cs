using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Unified.Calendar;
using Unified.Calendar.Services;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.LoanOut;
using Unified.LoanOut.Factories;
using Unified.LoanOut.Models;
using Unified.Scheduling.Services;

namespace Unified.LoanOut.Services;

/// <summary>
/// Exposes the LoanOut domain's registered entry and series operations through the shared calendar provider.
/// Register configures both pipelines with LoanOutEventFactory; CalendarModule supplies the generic provider.
/// </summary>
public sealed class LoanOutServiceProvider(
    CalendarEventServiceProvider<
        LoanOutEntryRequest,
        LoanOutEntry,
        LoanOutSeriesRequest,
        LoanOutSeries
    > calendarServices
)
{
    public bool SupportsEntry => calendarServices.SupportsEntry;

    public bool SupportsSeries => calendarServices.SupportsSeries;

    public ICalendarEventService<LoanOutEntryRequest, LoanOutEntry, Event> EntryService =>
        calendarServices.EntryService;

    public ICalendarEventSeriesService<LoanOutSeriesRequest, LoanOutSeries> SeriesService =>
        calendarServices.SeriesService;

    public static IServiceCollection Register(IServiceCollection services)
    {
        services.AddCalendarEventEntry<LoanOutEntryRequest, LoanOutEntry, LoanOutEventFactory>();
        services.AddCalendarEventSeries<LoanOutSeriesRequest, LoanOutSeries, LoanOutEventFactory>();
        services.AddScoped<ILoanOutEntryService, LoanOutEntryService>();
        services.AddScoped<ILoanOutSeriesService, LoanOutSeriesService>();
        services.Replace(
            ServiceDescriptor.Scoped<ISchedulingLoanOutService>(serviceProvider =>
                serviceProvider.GetService<SchedulingLoanOutService>() is { } schedulingService
                    ? schedulingService
                    : new NoOpSchedulingLoanOutService()
            )
        );
        services.TryAddScoped<LoanOutServiceProvider>();

        return services;
    }
}
