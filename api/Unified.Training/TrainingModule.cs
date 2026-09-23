using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Unified.Authorization;
using Unified.Common.Contracts;
using Unified.Common.Events;
using Unified.Common.FeatureFlags;
using Unified.Common.Jobs;
using Unified.Common.Options;
using Unified.Common.Reporting;
using Unified.Core.Services.Lookup;
using Unified.Training.FeatureFlags;
using Unified.Training.Jobs;
using Unified.Training.Options;
using Unified.Training.Handlers;
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

        services
            .AddOptions<TrainingExpiryNotificationOptions>()
            .BindConfiguration(TrainingExpiryNotificationOptions.SectionName);

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IEntitySideEffect<UserCreatedSignal>, AssignMandatoryTrainingOnUserCreationHandler>();
        services.AddScoped<IUserTrainingService, UserTrainingService>();
        services.AddScoped<IUserTrainingExpiryNotificationService, UserTrainingExpiryNotificationService>();
        services.AddScoped<IRecurringJob, UserTrainingExpiryNoticeRecurringJob>();
        services.AddScoped<ITrainingLookupStrategy, TrainingLookupStrategy>();
        services.AddScoped<IReportQueryHandler, UserTrainingReportQueryHandler>();
        services.AddScoped<ILookupStrategy>(serviceProvider =>
            serviceProvider.GetRequiredService<ITrainingLookupStrategy>()
        );

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
