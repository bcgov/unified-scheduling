using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Unified.Authorization;
using Unified.Calendar;
using Unified.Calendar.Services;
using Unified.Common.FeatureFlags;
using Unified.Common.Options;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Leave.FeatureFlags;
using Unified.Leave.Mappings;
using Unified.Leave.Models;
using Unified.Leave.Models.Calendar;
using Unified.Leave.Seeders;
using Unified.Leave.Services;
using Unified.Leave.Validators;

namespace Unified.Leave;

public static class LeaveModule
{
    public static bool IsModuleEnabled(IConfiguration config) =>
        config.GetSection(LeaveFeatureFlags.Section).Get<LeaveFeatureFlags>()?.Enabled ?? false;

    public static IServiceCollection AddLeaveModule(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddOptions<LeaveFeatureFlags>()
            .BindConfiguration(LeaveFeatureFlags.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<
            IValidateOptions<LeaveFeatureFlags>,
            RequiredBooleanOptionsValidator<LeaveFeatureFlags>
        >();
        services.AddSingleton<IFeatureFlags>(sp => sp.GetRequiredService<IOptions<LeaveFeatureFlags>>().Value);

        if (!IsModuleEnabled(config))
            return services;

        if (!CalendarModule.IsModuleEnabled(config))
            throw new InvalidOperationException("Leave requires the Calendar module to be enabled.");

        services.AddScoped<ISchedulingLeaveService, SchedulingLeaveService>();
        services.AddScoped<ISchedulingLeaveEntryService, SchedulingLeaveEntryService>();
        services.AddScoped<ISchedulingLeaveSeriesService, SchedulingLeaveSeriesService>();
        LeaveServiceProvider.Register(services);
        services.AddScoped<SchedulingLeaveEventMapper>();
        services.AddScoped<LeaveSeriesMaterializationHandler>();
        services.AddScoped<
            IEventSeriesMaterializationHandler<LeaveSeriesMaterializationContext>,
            LeaveSeriesMaterializationHandler
        >(sp => sp.GetRequiredService<LeaveSeriesMaterializationHandler>());
        services.AddScoped<SchedulingLeaveRequestValidator>();
        services.AddScoped<SchedulingLeaveSeriesRequestValidator>();
        services.AddScoped<LeaveTypeRequestValidator>();
        services.AddScoped<ILeaveCalendarDataProvider, LeaveCalendarDataProvider>();
        services.AddScoped<LeaveCalendarQueryValidator>();
        services.AddScoped<ILeaveTypeService, LeaveTypeService>();
        services.AddSeeder<UnifiedDbContext, LeaveTypeSeeder>();
        services.AddSeeder<UnifiedDbContext, SchedulingLeaveEventTypeSeeder>();

        services
            .AddAuthorizationBuilder()
            .AddPermissionPolicy(Permissions.LeaveView)
            .AddPermissionPolicy(Permissions.LeaveCreateAndAssign)
            .AddPermissionPolicy(Permissions.LeaveEdit)
            .AddPermissionPolicy(Permissions.LeaveDelete)
            .AddAnyPermissionPolicy(LeavePolicies.LeaveViewOrUser, Permissions.LeaveView, Permissions.UsersLeaveView)
            .AddAnyPermissionPolicy(
                LeavePolicies.LeaveCreateOrUser,
                Permissions.LeaveCreateAndAssign,
                Permissions.UsersLeaveCreate
            )
            .AddAnyPermissionPolicy(LeavePolicies.LeaveEditOrUser, Permissions.LeaveEdit, Permissions.UsersLeaveEdit)
            .AddAnyPermissionPolicy(
                LeavePolicies.LeaveDeleteOrUser,
                Permissions.LeaveDelete,
                Permissions.UsersLeaveDelete
            )
            .AddAnyPermissionPolicy(LeavePolicies.LeaveTypesView, Permissions.LeaveView, Permissions.UsersLeaveView);

        return services;
    }
}
