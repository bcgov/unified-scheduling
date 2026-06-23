using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Unified.Training.Services;

namespace Unified.Training;

public static class TrainingModule
{
    public static IServiceCollection AddTrainingModule(this IServiceCollection service)
    {
        service.AddScoped<ITrainingService, TrainingService>();

        return service;
    }

    public static IEndpointRouteBuilder MapTrainingEndpoints(this IEndpointRouteBuilder app)
    {
        var grpBuilder = app.MapGroup("/api/training").WithTags("Training");

        grpBuilder.MapGet(
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
