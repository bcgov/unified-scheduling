using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Unified.Authorization;
using Unified.Training.Controllers;
using Unified.Training.Services;
using Unified.Training.Validators;

namespace Unified.Training;

public static class TrainingModule
{
    public static IServiceCollection AddTrainingModule(this IServiceCollection services)
    {
        services.AddScoped<ITrainingService, TrainingService>();
        services.AddScoped<TrainingRequestValidator>();

        services.AddSingleton(TrainingPermissionSeedData.Configuration);

        services
            .AddAuthorizationBuilder()
            .AddPermissionPolicy(Permissions.TrainingsView)
            .AddPermissionPolicy(Permissions.TrainingsCreate)
            .AddPermissionPolicy(Permissions.TrainingsEdit)
            .AddPermissionPolicy(Permissions.TrainingsDelete)
            .AddPermissionPolicy(Permissions.TrainingRecordsManageForOthers)
            .AddPermissionPolicy(Permissions.TrainingEditPast)
            .AddPermissionPolicy(Permissions.TrainingRemovePast)
            .AddPermissionPolicy(Permissions.TrainingAdjustExpiry)
            .AddPermissionPolicy(Permissions.TrainingExempt);

        return services;
    }

    public static IEndpointRouteBuilder MapTrainingEndpoints(this IEndpointRouteBuilder app)
    {
        var grpBuilder = app.MapGroup("/api/training").WithTags("Training");

        grpBuilder
            .MapGet("/health", () => TypedResults.Ok("Training Loaded Successfully"))
            .WithName("GetTrainingHealth")
            .WithDescription("Checks the health of the Training module.");

        return app;
    }
}
