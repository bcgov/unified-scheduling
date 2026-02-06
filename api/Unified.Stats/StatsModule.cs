using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Unified.Stats.Services;

namespace Unified.Stats;

public static class StatsModule
{
    public static IServiceCollection AddStatsModule(this IServiceCollection s) => s
        .AddScoped<IStatsService, StatsService>();

    public static IEndpointRouteBuilder MapStatsEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/stats").WithTags("Stats");
        
        g.MapGet("/health", (IStatsService statsService) =>
        {
            var result = statsService.CheckHealth();
            return TypedResults.Ok(result);
        }).WithName("GetStatsHealth").WithDescription("Checks the health of the Stats module.");

        return app;
    }
}
