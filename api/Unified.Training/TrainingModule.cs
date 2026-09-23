using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Unified.Authorization;
using Unified.Common.FeatureFlags;
using Unified.Common.Interceptors;
using Unified.Common.Options;
using Unified.Common.Reporting;
using Unified.Common.Seeding;
using Unified.Core.Services.Lookup;
using Unified.Db;
using Unified.Training.FeatureFlags;
using Unified.Training.Rules;
using Unified.Training.Seeders;
using Unified.Training.Services;
using Unified.Training.Services.Lookup;
using Unified.Training.Services.Reporting;
using Unified.Training.Validators;

namespace Unified.Training;

public static class TrainingModule
{
    public static bool IsModuleEnabled(IConfiguration config)
    {
        var enabled = config.GetSection(TrainingFeatureFlags.Section).Get<TrainingFeatureFlags>()?.Enabled ?? false;
        return enabled;
    }

    public static bool IsModuleEnabled(IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<IOptions<TrainingFeatureFlags>>();
        return options.Value.Enabled;
    }

    public static IServiceCollection AddTrainingModule(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddOptions<TrainingFeatureFlags>()
            .BindConfiguration(TrainingFeatureFlags.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<
            IValidateOptions<TrainingFeatureFlags>,
            RequiredBooleanOptionsValidator<TrainingFeatureFlags>
        >();
        services.AddSingleton<IFeatureFlags>(sp => sp.GetRequiredService<IOptions<TrainingFeatureFlags>>().Value);

        if (!IsModuleEnabled(config))
        {
            return services;
        }

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IUserTrainingService, UserTrainingService>();
        services.AddScoped<ITrainingProfileTypeService, TrainingProfileTypeService>();
        services.AddScoped<ITrainingLookupStrategy, TrainingLookupStrategy>();
        services.AddScoped<IReportQueryHandler, UserTrainingReportQueryHandler>();
        services.AddScoped<ISaveRule, TrainingProfileTypeExistsRule>();
        services.AddScoped<ILookupStrategy>(serviceProvider =>
            serviceProvider.GetRequiredService<ITrainingLookupStrategy>()
        );
        services.AddSeeder<UnifiedDbContext, TrainingProfileSeeder>();

        services.AddScoped<TrainingLookupRequestValidator>();
        services.AddScoped<UserTrainingRequestValidator>();

        services
            .AddAuthorizationBuilder()
            .AddPermissionPolicy(Permissions.TrainingsView)
            .AddPermissionPolicy(Permissions.TrainingsCreate)
            .AddPermissionPolicy(Permissions.TrainingsEdit)
            .AddPermissionPolicy(Permissions.UserTrainingsView)
            .AddPermissionPolicy(Permissions.UserTrainingsCreate)
            .AddPermissionPolicy(Permissions.UserTrainingsEdit)
            .AddPermissionPolicy(Permissions.UserTrainingsDelete);

        return services;
    }
}
