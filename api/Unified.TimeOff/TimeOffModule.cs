using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Unified.Authorization;
using Unified.Calendar.Services;
using Unified.Common.FeatureFlags;
using Unified.Common.Options;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.TimeOff.FeatureFlags;
using Unified.TimeOff.Mappings;
using Unified.TimeOff.Models;
using Unified.TimeOff.Models.Calendar;
using Unified.TimeOff.Seeders;
using Unified.TimeOff.Services;
using Unified.TimeOff.Validators;

namespace Unified.TimeOff;

public static class TimeOffModule
{
    public static bool IsModuleEnabled(IConfiguration config) =>
        config.GetSection(TimeOffFeatureFlags.Section).Get<TimeOffFeatureFlags>()?.Enabled ?? false;

    public static IServiceCollection AddTimeOffModule(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddOptions<TimeOffFeatureFlags>()
            .BindConfiguration(TimeOffFeatureFlags.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<
            IValidateOptions<TimeOffFeatureFlags>,
            RequiredBooleanOptionsValidator<TimeOffFeatureFlags>
        >();
        services.AddSingleton<IFeatureFlags>(sp => sp.GetRequiredService<IOptions<TimeOffFeatureFlags>>().Value);

        if (!IsModuleEnabled(config))
            return services;

        services.AddScoped<ISchedulingTimeOffService, SchedulingTimeOffService>();
        services.AddScoped<TimeOffSeriesMaterializationHandler>();
        services.AddScoped<
            IEventSeriesMaterializationHandler<TimeOffSeriesMaterializationContext>,
            TimeOffSeriesMaterializationHandler
        >(sp => sp.GetRequiredService<TimeOffSeriesMaterializationHandler>());
        services.AddScoped<SchedulingTimeOffRequestValidator>();
        services.AddScoped<SchedulingTimeOffSeriesRequestValidator>();
        services.AddScoped<LeaveTypeRequestValidator>();
        services.AddScoped<ITimeOffCalendarDataProvider, TimeOffCalendarDataProvider>();
        services.AddScoped<ILeaveTypeService, LeaveTypeService>();
        services.AddSeeder<UnifiedDbContext, LeaveTypeSeeder>();
        services.AddSeeder<UnifiedDbContext, SchedulingTimeOffEventTypeSeeder>();

        services
            .AddAuthorizationBuilder()
            .AddPermissionPolicy(Permissions.TimeOffView)
            .AddPermissionPolicy(Permissions.TimeOffCreateAndAssign)
            .AddPermissionPolicy(Permissions.TimeOffEdit)
            .AddPermissionPolicy(Permissions.TimeOffDelete)
            .AddAnyPermissionPolicy(
                TimeOffPolicies.TimeOffViewOrUser,
                Permissions.TimeOffView,
                Permissions.UsersTimeOffView
            )
            .AddAnyPermissionPolicy(
                TimeOffPolicies.TimeOffCreateOrUser,
                Permissions.TimeOffCreateAndAssign,
                Permissions.UsersTimeOffCreate
            )
            .AddAnyPermissionPolicy(
                TimeOffPolicies.TimeOffEditOrUser,
                Permissions.TimeOffEdit,
                Permissions.UsersTimeOffEdit
            )
            .AddAnyPermissionPolicy(
                TimeOffPolicies.TimeOffDeleteOrUser,
                Permissions.TimeOffDelete,
                Permissions.UsersTimeOffDelete
            )
            .AddAnyPermissionPolicy(
                LeavePolicies.LeaveTypesView,
                Permissions.TimeOffView,
                Permissions.UsersTimeOffView
            );

        return services;
    }
}
