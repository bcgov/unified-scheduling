using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Unified.Calendar.Controllers;
using Unified.Calendar.Options;
using Unified.Calendar.Seeders;
using Unified.Calendar.Services;
using Unified.Calendar.Validators;
using Unified.Infrastructure.Modules;

namespace Unified.Calendar;

public static class CalendarModule
{
    public const string ModuleName = "CalendarModule";

    public static UnifiedModuleDescriptor Descriptor { get; } =
        new(ModuleName, featureFlags => featureFlags.CalendarModule, []);

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
        services
            .AddOptions<CalendarSeedDataOptions>()
            .Bind(configuration.GetSection(CalendarSeedDataOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<ICalendarEventService, CalendarEventService>();
        services.AddScoped<ICalendarDateTimeService, CalendarDateTimeService>();
        services.AddScoped<ICalendarLifecycleService, CalendarLifecycleService>();
        services.AddScoped<IRecurrenceExpander, IcalNetRecurrenceExpander>();
        services.AddScoped<IRecurrenceRuleValidator, IcalNetRecurrenceRuleValidator>();
        services.AddScoped<IEventSeriesMaterializationService, EventSeriesMaterializationService>();
        services.AddScoped<EventTypeSeeder>();
        services.AddScoped<EventStatusTypeSeeder>();
        services.AddScoped<HolidayEventSeeder>();
        services.AddScoped<CalendarDataRequestValidator>();

        return services;
    }

    private static bool BeValidDefaultTimeZoneId(CalendarDateTimeOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DefaultTimeZoneId))
            return false;

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(options.DefaultTimeZoneId.Trim());
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
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
