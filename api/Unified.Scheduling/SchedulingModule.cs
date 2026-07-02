using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Unified.Authorization;
using Unified.Calendar;
using Unified.Infrastructure.Modules;
using Unified.Scheduling.Controllers;
using Unified.Scheduling.Seeders;
using Unified.Scheduling.Services;
using Unified.Scheduling.Validators;

namespace Unified.Scheduling;

public static class SchedulingModule
{
    public const string ModuleName = "SchedulingModule";

    public static UnifiedModuleDescriptor Descriptor { get; } =
        new(ModuleName, featureFlags => featureFlags.SchedulingModule, [CalendarModule.ModuleName]);

    public static IMvcBuilder AddSchedulingApplicationPart(this IMvcBuilder mvcBuilder, bool isEnabled)
    {
        var schedulingAssembly = typeof(ShiftController).Assembly;

        mvcBuilder.ConfigureApplicationPartManager(manager =>
            ConfigureSchedulingApplicationParts(manager, schedulingAssembly, isEnabled)
        );

        return mvcBuilder;
    }

    public static IServiceCollection AddSchedulingModule(this IServiceCollection services)
    {
        services.AddScoped<IShiftService, ShiftService>();
        services.AddScoped<ShiftSeriesMaterializationHandler>();
        services.AddScoped<ShiftSeriesRequestValidator>();
        services.AddScoped<ShiftEntryRequestValidator>();
        services.AddScoped<SchedulingCalendarRequestValidator>();
        services.AddScoped<ShiftEventTypeSeeder>();

        services
            .AddAuthorizationBuilder()
            .AddPermissionPolicy(Permissions.ShiftsView)
            .AddPermissionPolicy(Permissions.ShiftsCreateAndAssign)
            .AddPermissionPolicy(Permissions.ShiftsEdit)
            .AddPermissionPolicy(Permissions.ShiftsExpire)
            .AddPermissionPolicy(Permissions.ShiftsImport)
            .AddPermissionPolicy(Permissions.ShiftsViewAllFuture);

        return services;
    }

    private static void ConfigureSchedulingApplicationParts(
        ApplicationPartManager manager,
        Assembly schedulingAssembly,
        bool isEnabled
    )
    {
        var assemblyName = schedulingAssembly.GetName().Name;
        var existingParts = manager.ApplicationParts.Where(part => part.Name == assemblyName).ToList();

        foreach (var part in existingParts)
        {
            manager.ApplicationParts.Remove(part);
        }

        if (isEnabled)
        {
            manager.ApplicationParts.Add(new AssemblyPart(schedulingAssembly));
        }
    }
}
