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
        services.AddScoped<ITrainingsService, TrainingsService>();
        services.AddScoped<TrainingRequestValidator>();

        services.AddSingleton(TrainingPermissionSeedData.Configuration);

        services
            .AddAuthorizationBuilder()
            .AddPermissionPolicy(Permissions.TrainingsView.ToString())
            .AddPermissionPolicy(Permissions.TrainingsCreate.ToString())
            .AddPermissionPolicy(Permissions.TrainingsEdit.ToString())
            .AddPermissionPolicy(Permissions.TrainingsDelete.ToString())
            .AddPermissionPolicy(Permissions.TrainingRecordsManageForOthers.ToString())
            .AddPermissionPolicy(Permissions.TrainingEditPast.ToString())
            .AddPermissionPolicy(Permissions.TrainingRemovePast.ToString())
            .AddPermissionPolicy(Permissions.TrainingAdjustExpiry.ToString())
            .AddPermissionPolicy(Permissions.TrainingExempt.ToString());

        return services;
    }

    public static IEndpointRouteBuilder MapTrainingEndpoints(this IEndpointRouteBuilder app)
    {
        var grpBuilder = app.MapGroup("/api/training").WithTags("Training");

        grpBuilder
            .MapGet(
                "/health",
                (ITrainingService trainingService) =>
                {
                    var result = trainingService.CheckHealth();
                    return TypedResults.Ok(result);
                }
            )
            .WithName("GetTrainingHealth")
            .WithDescription("Checks the health of the Training module.");

        return app;
    }
}
