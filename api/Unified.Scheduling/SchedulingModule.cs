using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Unified.Authorization;
using Unified.Authorization.Claims;
using Unified.Authorization.Requirements;
using Unified.Calendar;
using Unified.Calendar.Conflicts;
using Unified.Calendar.Services;
using Unified.Common.Calendar.Conflicts;
using Unified.Common.FeatureFlags;
using Unified.Common.Options;
using Unified.Common.Seeding;
using Unified.Common.Time;
using Unified.Db;
using Unified.Scheduling.FeatureFlags;
using Unified.Scheduling.Mappings;
using Unified.Scheduling.Options;
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

        services
            .AddOptions<WorkingHoursOptions>()
            .BindConfiguration(WorkingHoursOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        if (!IsModuleEnabled(config))
            return services;

        if (!CalendarModule.IsModuleEnabled(config))
            throw new InvalidOperationException("Scheduling requires the Calendar module to be enabled.");
        if (!StatsModule.IsModuleEnabled(config))
            throw new InvalidOperationException("Scheduling requires the Stats module to be enabled.");

        services.AddSingleton(TimeProvider.System);
        services.TryAddScoped<ISchedulingLoanOutService, NoOpSchedulingLoanOutService>();
        services.TryAddScoped<ShiftLoanOutAvailabilityService>(provider =>
            new(
                provider.GetRequiredService<UnifiedDbContext>(),
                provider.GetRequiredService<ICalendarTimeZoneResolver>(),
                provider.GetRequiredService<ITimeZoneService>(),
                () => provider.GetRequiredService<IShiftService>(),
                config.GetValue<bool>("FeatureFlags:LoanOut:Enabled")
            )
        );
        services.TryAddScoped<ShiftLoanOutExceptionMarkerService>();
        services.TryAddScoped<SchedulingLoanOutEventMapper>();
        services.TryAddScoped<LoanOutShiftTransferService>(provider =>
            new(
                provider.GetRequiredService<ILogger<LoanOutShiftTransferService>>(),
                provider.GetRequiredService<UnifiedDbContext>(),
                provider.GetRequiredService<SchedulingLoanOutEventMapper>(),
                () => provider.GetRequiredService<IShiftService>(),
                () => provider.GetRequiredService<IShiftAssignmentService>(),
                provider.GetRequiredService<ShiftLoanOutAvailabilityService>(),
                provider.GetRequiredService<ShiftLoanOutExceptionMarkerService>(),
                provider.GetRequiredService<IRecurrenceExpander>()
            )
        );
        services.TryAddScoped<SchedulingLoanOutService>();
        services.AddScoped<IShiftEntryService, ShiftEntryService>();
        services.AddScoped<IShiftSeriesService, ShiftSeriesService>();
        services.AddScoped<ShiftService>();
        services.AddScoped<IShiftService>(serviceProvider => serviceProvider.GetRequiredService<ShiftService>());
        services.AddScoped<IShiftPublicationService>(serviceProvider =>
            serviceProvider.GetRequiredService<ShiftService>()
        );
        services.AddScoped<ISchedulingCalendarService, SchedulingCalendarService>();
        services.AddScoped<ICalendarConflictParticipantProvider, SchedulingConflictParticipantProvider>();
        services.AddScoped<IAssignmentEntryService, AssignmentEntryService>();
        services.AddScoped<IAssignmentSeriesService, AssignmentSeriesService>();
        services.AddScoped<AssignmentService>();
        services.AddScoped<IAssignmentService>(serviceProvider =>
            serviceProvider.GetRequiredService<AssignmentService>()
        );
        services.AddScoped<IAssignmentPublicationService>(serviceProvider =>
            serviceProvider.GetRequiredService<AssignmentService>()
        );
        services.AddScoped<ISchedulePublishService, SchedulePublishService>();
        services.AddScoped<IScheduleDistributionService, ScheduleDistributionService>();
        services.AddScoped<IAssignmentDefinitionService, AssignmentDefinitionService>();
        services.AddScoped<IShiftAssignmentService, ShiftAssignmentService>();
        services.AddScoped<IProposedShiftAssignmentOptionsService, ProposedShiftAssignmentOptionsService>();
        services.AddScoped<IWorkingHoursService, WorkingHoursService>();
        services.AddScoped<WorkingHoursQueryValidator>();
        services.AddScoped<ShiftConflictService>();
        SchedulingServiceProvider.Register(services);
        services.AddScoped<ShiftSeriesMaterializationHandler>();
        services.AddScoped<AssignmentSeriesMaterializationHandler>();
        services.AddScoped<ShiftSeriesRequestValidator>();
        services.AddScoped<ShiftEntryRequestValidator>();
        services.AddScoped<ShiftEntryUpdateRequestValidator>();
        services.AddScoped<AssignmentSeriesRequestValidator>();
        services.AddScoped<AssignmentEntryRequestValidator>();
        services.AddScoped<AssignmentEntryUpdateRequestValidator>();
        services.AddScoped<AssignmentDefinitionRequestValidator>();
        services.AddScoped<ShiftAssignmentEntryRequestValidator>();
        services.AddScoped<ShiftAssignmentSeriesRequestValidator>();
        services.AddScoped<ShiftAssignmentEntryUpdateRequestValidator>();
        services.AddScoped<ShiftAssignmentSeriesUpdateRequestValidator>();
        services.AddScoped<ProposedShiftAssignmentOptionsRequestValidator>();
        services.AddScoped<SchedulingCalendarRequestValidator>();
        services.AddScoped<SchedulePublishRequestValidator>();
        services.AddScoped<ScheduleDistributionRequestValidator>();
        services.AddSeeder<Unified.Db.UnifiedDbContext, ShiftEventTypeSeeder>();
        services.AddSingleton(SchedulingPermissionSeedData.Configuration);

        services
            .AddAuthorizationBuilder()
            .AddPolicy(
                SchedulingPolicies.SchedulePublishWithView,
                policy =>
                    policy
                        .RequireAuthenticatedUser()
                        .AddRequirements(new PermissionRequirement(nameof(Permissions.SchedulePublish)))
                        .RequireAssertion(context =>
                            context.User.HasClaim(UnifiedClaimTypes.Permission, nameof(Permissions.ShiftsView))
                            || context.User.HasClaim(UnifiedClaimTypes.Permission, nameof(Permissions.AssignmentsView))
                        )
            )
            .AddPolicy(
                SchedulingPolicies.ScheduleDistributeWithView,
                policy =>
                    policy
                        .RequireAuthenticatedUser()
                        .AddRequirements(new PermissionRequirement(nameof(Permissions.ScheduleViewDistribute)))
                        .RequireAssertion(context =>
                            context.User.HasClaim(UnifiedClaimTypes.Permission, nameof(Permissions.ShiftsView))
                            || context.User.HasClaim(UnifiedClaimTypes.Permission, nameof(Permissions.AssignmentsView))
                        )
            )
            .AddPermissionPolicy(Permissions.ShiftsView)
            .AddPermissionPolicy(Permissions.ShiftsCreateAndAssign)
            .AddPermissionPolicy(Permissions.ShiftsEdit)
            .AddPermissionPolicy(Permissions.ShiftsDelete)
            .AddPermissionPolicy(Permissions.ShiftsExpire)
            .AddPermissionPolicy(Permissions.ScheduleViewDistribute)
            .AddPermissionPolicy(Permissions.SchedulePublish)
            .AddPermissionPolicy(Permissions.AssignmentsView)
            .AddPermissionPolicy(Permissions.AssignmentsCreate)
            .AddPermissionPolicy(Permissions.AssignmentsAssign)
            .AddPermissionPolicy(Permissions.AssignmentsEdit)
            .AddPermissionPolicy(Permissions.AssignmentsDelete)
            .AddPermissionPolicy(Permissions.AssignmentsExpire);

        return services;
    }
}
