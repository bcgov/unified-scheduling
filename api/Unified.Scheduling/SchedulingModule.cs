using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Unified.Authorization;
using Unified.Calendar;
using Unified.Common.FeatureFlags;
using Unified.Common.Options;
using Unified.Common.Seeding;
using Unified.Scheduling.FeatureFlags;
using Unified.Scheduling.Seeders;
using Unified.Scheduling.Services;
using Unified.Scheduling.Validators;

namespace Unified.Scheduling;

public static class SchedulingModule
{
    public static bool IsModuleEnabled(IConfiguration config) =>
        config.GetSection(SchedulingFeatureFlags.Section).Get<SchedulingFeatureFlags>()?.Enabled ?? false;

    public static bool IsModuleEnabled(IServiceProvider serviceProvider) =>
        serviceProvider.GetRequiredService<IOptions<SchedulingFeatureFlags>>().Value.Enabled;

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

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IShiftService, ShiftService>();
        services.AddScoped<ShiftSeriesMaterializationHandler>();
        services.AddScoped<ShiftSeriesRequestValidator>();
        services.AddScoped<ShiftEntryRequestValidator>();
        services.AddScoped<SchedulingCalendarRequestValidator>();
        services.AddScoped<ShiftEventTypeSeeder>();
        services.AddSingleton(SchedulingPermissionSeedData.Configuration);

        services
            .AddAuthorizationBuilder()
            .AddPermissionPolicy(Permissions.ShiftsView)
            .AddPermissionPolicy(Permissions.ShiftsCreateAndAssign)
            .AddPermissionPolicy(Permissions.ShiftsEdit)
            .AddPermissionPolicy(Permissions.ShiftsDelete)
            .AddPermissionPolicy(Permissions.ShiftsExpire);

        return services;
    }
}
