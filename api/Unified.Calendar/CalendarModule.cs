using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Unified.Calendar.Controllers;
using Unified.Calendar.Options;
using Unified.Calendar.Seeders;
using Unified.Calendar.Services;
using Unified.Calendar.Validators;

namespace Unified.Calendar;

public static class CalendarModule
{
    public static IMvcBuilder AddCalendarApplicationPart(this IMvcBuilder mvcBuilder, bool isEnabled)
    {
        var calendarAssembly = typeof(CalendarController).Assembly;

        mvcBuilder.ConfigureApplicationPartManager(manager =>
            ConfigureCalendarApplicationParts(manager, calendarAssembly, isEnabled)
        );

        return mvcBuilder;
    }

    public static IServiceCollection AddCalendarModule(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<CalendarSeedDataOptions>()
            .Bind(configuration.GetSection(CalendarSeedDataOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<ICalendarEventService, CalendarEventService>();
        services.AddScoped<EventTypeSeeder>();
        services.AddScoped<EventStatusTypeSeeder>();
        services.AddScoped<HolidayEventSeeder>();
        services.AddScoped<CalendarEventsRequestValidator>();

        return services;
    }

    private static void ConfigureCalendarApplicationParts(
        ApplicationPartManager manager,
        Assembly calendarAssembly,
        bool isEnabled
    )
    {
        var assemblyName = calendarAssembly.GetName().Name;
        var existingParts = manager.ApplicationParts.Where(part => part.Name == assemblyName).ToList();

        foreach (var part in existingParts)
        {
            manager.ApplicationParts.Remove(part);
        }

        if (isEnabled)
        {
            manager.ApplicationParts.Add(new AssemblyPart(calendarAssembly));
        }
    }
}
