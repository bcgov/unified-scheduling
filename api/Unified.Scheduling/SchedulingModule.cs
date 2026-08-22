using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Unified.Authorization;
using Unified.Calendar;
using Unified.Common.FeatureFlags;
using Unified.Common.Options;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Scheduling.Controllers;
using Unified.Scheduling.FeatureFlags;
using Unified.Scheduling.Seeders;
using Unified.Scheduling.Services;
using Unified.Scheduling.Validators;
using Unified.Stats;

namespace Unified.Scheduling;

public static class SchedulingModule
{
    public static bool IsModuleEnabled(IConfiguration config) =>
        config.GetSection(SchedulingFeatureFlags.Section).Get<SchedulingFeatureFlags>()?.Enabled ?? false;

    public static bool IsModuleEnabled(IServiceProvider serviceProvider) =>
        serviceProvider.GetRequiredService<IOptions<SchedulingFeatureFlags>>().Value.Enabled;

    public static IMvcBuilder AddSchedulingApplicationPart(this IMvcBuilder mvcBuilder, IConfiguration config)
    {
        var isEnabled = IsModuleEnabled(config);
        var schedulingAssembly = typeof(ShiftController).Assembly;

        mvcBuilder.ConfigureApplicationPartManager(manager =>
            ConfigureSchedulingApplicationParts(manager, schedulingAssembly, isEnabled)
        );

        return mvcBuilder;
    }

    public static IServiceCollection AddSchedulingModule(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddOptions<SchedulingFeatureFlags>()
            .BindConfiguration(SchedulingFeatureFlags.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<
            IValidateOptions<SchedulingFeatureFlags>,
            RequiredBooleanOptionsValidator<SchedulingFeatureFlags>
        >();
        services.AddSingleton<IFeatureFlags>(serviceProvider =>
            serviceProvider.GetRequiredService<IOptions<SchedulingFeatureFlags>>().Value
        );

        if (!IsModuleEnabled(config))
            return services;

        if (!CalendarModule.IsModuleEnabled(config))
            throw new InvalidOperationException("Scheduling requires the Calendar module to be enabled.");
        if (!StatsModule.IsModuleEnabled(config))
            throw new InvalidOperationException("Scheduling requires the Stats module to be enabled.");

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IShiftService, ShiftService>();
        services.AddScoped<ISchedulingCalendarService, SchedulingCalendarService>();
        services.AddScoped<IAssignmentService, AssignmentService>();
        services.AddScoped<IAssignmentDefinitionService, AssignmentDefinitionService>();
        services.AddScoped<IShiftAssignmentService, ShiftAssignmentService>();
        services.AddScoped<ShiftSeriesMaterializationHandler>();
        services.AddScoped<AssignmentSeriesMaterializationHandler>();
        services.AddScoped<ShiftSeriesRequestValidator>();
        services.AddScoped<ShiftEntryRequestValidator>();
        services.AddScoped<AssignmentSeriesRequestValidator>();
        services.AddScoped<AssignmentEntryRequestValidator>();
        services.AddScoped<AssignmentEntryUpdateRequestValidator>();
        services.AddScoped<AssignmentDefinitionRequestValidator>();
        services.AddScoped<ShiftAssignmentEntryRequestValidator>();
        services.AddScoped<ShiftAssignmentSeriesRequestValidator>();
        services.AddScoped<ShiftAssignmentEntryUpdateRequestValidator>();
        services.AddScoped<ShiftAssignmentSeriesUpdateRequestValidator>();
        services.AddScoped<SchedulingCalendarRequestValidator>();
        services.AddSeeder<UnifiedDbContext, ShiftEventTypeSeeder>();
        services.AddSingleton(SchedulingPermissionSeedData.Configuration);

        services
            .AddAuthorizationBuilder()
            .AddPermissionPolicy(Permissions.ShiftsView)
            .AddPermissionPolicy(Permissions.ShiftsCreateAndAssign)
            .AddPermissionPolicy(Permissions.ShiftsEdit)
            .AddPermissionPolicy(Permissions.ShiftsDelete)
            .AddPermissionPolicy(Permissions.ShiftsExpire)
            .AddPermissionPolicy(Permissions.AssignmentsView)
            .AddPermissionPolicy(Permissions.AssignmentsCreate)
            .AddPermissionPolicy(Permissions.AssignmentsAssign)
            .AddPermissionPolicy(Permissions.AssignmentsEdit)
            .AddPermissionPolicy(Permissions.AssignmentsDelete)
            .AddPermissionPolicy(Permissions.AssignmentsExpire);

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
