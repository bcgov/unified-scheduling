using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Unified.Calendar.Factories;
using Unified.Calendar.Services;
using Unified.Common.Calendar.Scheduling.Models;

namespace Unified.Calendar;

/// <summary>
/// Registers a domain's supported calendar operations. Use alongside AddCalendarModule;
/// domains supply their factories while the calendar module supplies shared dependencies.
/// Register only supported operations. Repeated calls preserve existing registrations;
/// entry/series services and factories share instances within each DI scope.
/// </summary>
public static class CalendarEventServiceCollectionExtensions
{
    public static IServiceCollection AddCalendarEventEntry<TRequest, TEntity, TFactory>(
        this IServiceCollection services
    )
        where TRequest : CalendarEventEntryRequest
        where TFactory : class, ICalendarEventEntryFactory<TRequest, TEntity>
    {
        services.TryAddScoped<TFactory>();
        services.TryAddScoped<ICalendarEventEntryFactory<TRequest, TEntity>>(sp => sp.GetRequiredService<TFactory>());
        services.TryAddScoped<ICalendarEventService<TRequest, TEntity>>(sp =>
            sp.GetRequiredService<CalendarEventEntryService<TRequest, TEntity>>()
        );
        services.TryAddScoped<CalendarEventEntryService<TRequest, TEntity>>();
        return services;
    }

    public static IServiceCollection AddCalendarEventSeries<TRequest, TEntity, TFactory>(
        this IServiceCollection services
    )
        where TRequest : CalendarEventSeriesRequest
        where TFactory : class, ICalendarEventSeriesFactory<TRequest, TEntity>
    {
        services.TryAddScoped<TFactory>();
        services.TryAddScoped<ICalendarEventSeriesFactory<TRequest, TEntity>>(sp => sp.GetRequiredService<TFactory>());
        services.TryAddScoped<ICalendarEventService<TRequest, TEntity>>(sp =>
            sp.GetRequiredService<CalendarEventSeriesService<TRequest, TEntity>>()
        );
        services.TryAddScoped<CalendarEventSeriesService<TRequest, TEntity>>();
        return services;
    }
}
