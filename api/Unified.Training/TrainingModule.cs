using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Unified.Authorization;
using Unified.Common.FeatureFlags;
using Unified.Core.Services.Lookup;
using Unified.Training.FeatureFlags;
using Unified.Training.Services.Lookup;
using Unified.Training.Validators;

namespace Unified.Training;

public static class TrainingModule
{
    public static bool IsModuleEnabled(IServiceCollection services)
    {
        using var serviceProvider = services.BuildServiceProvider();
        return IsModuleEnabled(serviceProvider);
    }

    public static bool IsModuleEnabled(IServiceProvider serviceProvider)
    {
        var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<TrainingFeatureFlags>>();
        return optionsMonitor.CurrentValue.Enabled;
    }

    public static IMvcBuilder AddTrainingApplicationPart(this IMvcBuilder mvcBuilder, IServiceCollection services)
    {
        var isEnabled = IsModuleEnabled(services);
        var trainingAssembly = typeof(TrainingModule).Assembly;

        mvcBuilder.ConfigureApplicationPartManager(manager =>
            ConfigureTrainingApplicationParts(manager, trainingAssembly, isEnabled)
        );

        return mvcBuilder;
    }

    public static IServiceCollection AddTrainingModule(this IServiceCollection services)
    {
        services
            .AddOptions<TrainingFeatureFlags>()
            .BindConfiguration(TrainingFeatureFlags.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IFeatureFlags>(sp =>
            sp.GetRequiredService<IOptionsMonitor<TrainingFeatureFlags>>().CurrentValue
        );

        if (!IsModuleEnabled(services))
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
