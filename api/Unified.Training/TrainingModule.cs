using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Unified.Authorization;
using Unified.Common.FeatureFlags;
using Unified.Common.Options;
using Unified.Core.Services.Lookup;
using Unified.Training.FeatureFlags;
using Unified.Training.Services.Lookup;
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

    public static IMvcBuilder AddTrainingApplicationPart(this IMvcBuilder mvcBuilder, IConfiguration config)
    {
        var isEnabled = IsModuleEnabled(config);
        var trainingAssembly = typeof(TrainingModule).Assembly;

        mvcBuilder.ConfigureApplicationPartManager(manager =>
            ConfigureTrainingApplicationParts(manager, trainingAssembly, isEnabled)
        );

        return mvcBuilder;
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

        services.AddScoped<ITrainingLookupStrategy, TrainingLookupStrategy>();
        services.AddScoped<ILookupStrategy>(serviceProvider =>
            serviceProvider.GetRequiredService<ITrainingLookupStrategy>()
        );

        services.AddScoped<TrainingLookupRequestValidator>();

        services
            .AddAuthorizationBuilder()
            .AddPermissionPolicy(Permissions.TrainingsView)
            .AddPermissionPolicy(Permissions.TrainingsCreate)
            .AddPermissionPolicy(Permissions.TrainingsEdit)
            .AddPermissionPolicy(Permissions.TrainingsDelete)
            .AddPermissionPolicy(Permissions.TrainingsRecordsManageForOthers)
            .AddPermissionPolicy(Permissions.TrainingsEditPast)
            .AddPermissionPolicy(Permissions.TrainingsRemovePast)
            .AddPermissionPolicy(Permissions.TrainingsAdjustExpiry);

        return services;
    }

    public static IEndpointRouteBuilder MapTrainingEndpoints(this IEndpointRouteBuilder app)
    {
        if (!IsModuleEnabled(app.ServiceProvider))
        {
            return app;
        }

        var grpBuilder = app.MapGroup("/api/trainings").WithTags("Training");

        grpBuilder
            .MapGet("/health", () => TypedResults.Ok("Training Loaded Successfully"))
            .WithName("GetTrainingHealth")
            .WithDescription("Checks the health of the Training module.");

        return app;
    }

    private static void ConfigureTrainingApplicationParts(
        ApplicationPartManager manager,
        Assembly trainingAssembly,
        bool isEnabled
    )
    {
        var assemblyName = trainingAssembly.GetName().Name;
        var existingParts = manager.ApplicationParts.Where(part => part.Name == assemblyName).ToList();

        foreach (var part in existingParts)
        {
            manager.ApplicationParts.Remove(part);
        }

        if (isEnabled)
        {
            manager.ApplicationParts.Add(new AssemblyPart(trainingAssembly));
        }
    }
}
