using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Unified.Calendar.Controllers;
using Unified.Calendar.Holidays;
using Unified.Calendar.Options;
using Unified.Calendar.Seeders;
using Unified.Calendar.Services;
using Unified.Calendar.Validators;
using Unified.Common.Seeding;
using Unified.Common.Time;
using Unified.Db;

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
            .AddOptions<CalendarDateTimeOptions>()
            .Bind(configuration.GetSection(CalendarDateTimeOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(BeValidDefaultTimeZoneId, "CalendarDateTime:DefaultTimeZoneId must be a valid system time zone.")
            .ValidateOnStart();

        services.AddSingleton<IStatutoryHolidayCalculator, BcStatutoryHolidayCalculator>();
        services.AddSingleton<ITimeZoneService, TimeZoneService>();
        services.AddSingleton<StatutoryHolidayCalendarDataProvider>();
        services.AddScoped<ICalendarTimeZoneResolver, CalendarTimeZoneResolver>();
        services.AddScoped<ICalendarEventService, CalendarEventService>();
        services.AddSeeder<UnifiedDbContext, EventTypeSeeder>();
        services.AddSeeder<UnifiedDbContext, EventStatusTypeSeeder>();
        services.AddScoped<CalendarEventsRequestValidator>();

        return services;
    }

    private static bool BeValidDefaultTimeZoneId(CalendarDateTimeOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DefaultTimeZoneId))
            return false;

        return TimeZoneService.IsValidTimeZoneId(options.DefaultTimeZoneId);
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
