using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Unified.Calendar.Controllers;
using Unified.Calendar.FeatureFlags;
using Unified.Calendar.Options;
using Unified.Calendar.Seeders;
using Unified.Calendar.Services;
using Unified.Calendar.Validators;
using Unified.Common.FeatureFlags;
using Unified.Common.Seeding;
using Unified.Db;

namespace Unified.Calendar;

public static class CalendarModule
{
    public static bool IsModuleEnabled(IConfiguration config)
    {
        var enabled = config
            .GetSection(CalendarFeatureFlags.Section)
            .Get<CalendarFeatureFlags>()?
            .Enabled ?? false;
        return enabled;
    }

    public static bool IsModuleEnabled(IServiceProvider serviceProvider)
    {
        var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<CalendarFeatureFlags>>();
        return optionsMonitor.CurrentValue.Enabled;
    }

    public static IMvcBuilder AddCalendarApplicationPart(this IMvcBuilder mvcBuilder, IConfiguration config)
    {
        var isEnabled = IsModuleEnabled(config);
        var calendarAssembly = typeof(CalendarController).Assembly;

        mvcBuilder.ConfigureApplicationPartManager(manager =>
            ConfigureCalendarApplicationParts(manager, calendarAssembly, isEnabled)
        );

        return mvcBuilder;
    }

    public static IServiceCollection AddCalendarModule(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddOptions<CalendarFeatureFlags>()
            .BindConfiguration(CalendarFeatureFlags.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IFeatureFlags>(sp =>
            sp.GetRequiredService<IOptionsMonitor<CalendarFeatureFlags>>().CurrentValue
        );

        if (!IsModuleEnabled(config))
        {
            return services;
        }

        services
            .AddOptions<CalendarSeedDataOptions>()
            .BindConfiguration(CalendarSeedDataOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<ICalendarEventService, CalendarEventService>();
        services.AddSeeder<UnifiedDbContext, EventTypeSeeder>();
        services.AddSeeder<UnifiedDbContext, EventStatusTypeSeeder>();
        services.AddSeeder<UnifiedDbContext, HolidayEventSeeder>();
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
