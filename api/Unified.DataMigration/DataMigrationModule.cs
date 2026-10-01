using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Unified.DataMigration.FeatureFlags;
using Unified.DataMigration.Options;
using Unified.DataMigration.Services;

namespace Unified.DataMigration;

public static class DataMigrationModule
{
    public static IServiceCollection AddDataMigrationModule(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .AddOptions<DataMigrationFeatureFlags>()
            .BindConfiguration(DataMigrationFeatureFlags.Section)
            .ValidateOnStart();

        services
            .AddOptions<DataMigrationOptions>()
            .BindConfiguration(DataMigrationOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<DataMigrationOptions>, DataMigrationOptionsValidator>();

        services.AddScoped<DataMigrationOrchestrator>();
        services.AddScoped<DataMigrationControlService>();

        return services;
    }
}
