using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Unified.Stats.Services;
using Unified.Stats.Validators;

namespace Unified.Stats;

public static class StatsModule
{
    public static IServiceCollection AddStatsModule(this IServiceCollection s)
    {
        // Health check
        s.AddScoped<IStatsService, StatsService>();

        // Reference data services (read-only — managed via seeders)
        s.AddScoped<IStatGroupService, StatGroupService>();
        s.AddScoped<IStatCategoryService, StatCategoryService>();
        s.AddScoped<ISubCategoryService, SubCategoryService>();
        s.AddScoped<IStatMetricService, StatMetricService>();
        s.AddScoped<ISubCategoryMetricService, SubCategoryMetricService>();

        // Data entry services
        s.AddScoped<IStatRecordService, StatRecordService>();
        s.AddScoped<IStatSignoffService, StatSignoffService>();

        // Validators (data entry only)
        s.AddScoped<StatRecordRequestValidator>();
        s.AddScoped<StatSignoffRequestValidator>();

        return s;
    }

    public static IEndpointRouteBuilder MapStatsEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/stats").WithTags("Stats");

        g.MapGet(
                "/health",
                (IStatsService statsService) =>
                {
                    var result = statsService.CheckHealth();
                    return TypedResults.Ok(result);
                }
            )
            .WithName("GetStatsHealth")
            .WithDescription("Checks the health of the Stats module.");

        return app;
    }
}
