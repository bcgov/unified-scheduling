using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Unified.Authorization;
using Unified.Common.PostSave;
using Unified.Core.Services.Lookup;
using Unified.Training.Services;
using Unified.Training.Services.Lookup;
using Unified.Training.Validators;
using TrainingEntity = Unified.Db.Models.Training.Training;

namespace Unified.Training;

public static class TrainingModule
{
    public static IMvcBuilder AddTrainingApplicationPart(this IMvcBuilder mvcBuilder, bool isEnabled)
    {
        var trainingAssembly = typeof(TrainingModule).Assembly;

        mvcBuilder.ConfigureApplicationPartManager(manager =>
            ConfigureTrainingApplicationParts(manager, trainingAssembly, isEnabled)
        );

        return mvcBuilder;
    }

    public static IServiceCollection AddTrainingModule(this IServiceCollection services)
    {
        services.AddScoped<IUserTrainingService, UserTrainingService>();
        services.AddScoped<IEntityPostSaveDispatcher, EntityPostSaveDispatcher>();
        services.AddScoped<IEntityPostCreateHandler<TrainingEntity>, MandatoryTrainingHandler>();
        services.AddScoped<IEntityPostUpdateHandler<TrainingEntity>, MandatoryTrainingHandler>();
        services.AddScoped<ITrainingLookupStrategy, TrainingLookupStrategy>();
        services.AddScoped<ILookupStrategy>(serviceProvider =>
            serviceProvider.GetRequiredService<ITrainingLookupStrategy>()
        );

        services.AddScoped<TrainingLookupRequestValidator>();
        services.AddScoped<UserTrainingRequestValidator>();

        services.AddSingleton(TrainingPermissionSeedData.Configuration);

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

    public static IEndpointRouteBuilder MapTrainingEndpoints(this IEndpointRouteBuilder app)
    {
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
