using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Unified.Authorization;
using Unified.Common.Jobs;
using Unified.DataMigration.FeatureFlags;
using Unified.DataMigration.Jobs;
using Unified.DataMigration.Options;
using Unified.DataMigration.Services;
using Unified.DataMigration.Sources;

namespace Unified.DataMigration;

public static class DataMigrationModule
{
    public static bool IsModuleEnabled(IConfiguration config) =>
        config.GetSection(DataMigrationFeatureFlags.Section).Get<DataMigrationFeatureFlags>()?.Enabled ?? false;

    public static IServiceCollection AddDataMigrationModule(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .AddOptions<DataMigrationFeatureFlags>()
            .BindConfiguration(DataMigrationFeatureFlags.Section)
            .ValidateOnStart();

        services.AddHostedService<SsLegacyScheduleCleanup>();

        if (!IsModuleEnabled(configuration))
        {
            return services;
        }

        services
            .AddOptions<DataMigrationOptions>()
            .BindConfiguration(DataMigrationOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<DataMigrationOptions>, DataMigrationOptionsValidator>();

        services.AddScoped<DataMigrationOrchestrator>();
        services.AddScoped<DataMigrationControlService>();
        services.AddScoped<IMigrationRunCoordinator>(sp => sp.GetRequiredService<DataMigrationControlService>());
        services.AddHttpContextAccessor();
        services.AddScoped<DataMigrationOperations>();
        services.AddScoped<IMigrationJobDispatcher, MigrationJobDispatcher>();
        services.AddSingleton(MigrationPermissions.Configuration);
        services
            .AddAuthorizationBuilder()
            .AddPermissionPolicy(MigrationPermissions.Operate)
            .AddPermissionPolicy(MigrationPermissions.Rollback);
        services.AddScoped<SsSnapshotReader>();
        services.AddScoped<SsMigrationExecutor>();
        services.AddScoped<SsRollbackJournal>();
        services.AddScoped<SsRollbackService>();
        services.AddScoped<SsLeaveWriter>();
        services.AddScoped<SsMyTeamWriter>();
        services.AddScoped<SsTrainingWriter>();
        services.AddScoped<SsSchedulingWriter>();
        services.AddScoped<IRecurringJob, SsDataMigrationRecurringJob>();
        services.AddScoped<IRecurringJob, SsTrainingRecurringJob>();
        services.AddScoped<IRecurringJob, SsSchedulingRecurringJob>();
        services.AddScoped<IRecurringJob, SsReportRecurringJob>();
        services.AddScoped<ISsLegacyMigrationSource, SsLegacyMigrationSource>();

        return services;
    }
}
