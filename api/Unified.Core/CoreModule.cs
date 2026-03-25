using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Unified.Core.Seeders;

namespace Unified.Core;

public static class CoreModule
{
    public static IServiceCollection AddCoreModule(this IServiceCollection services)
    {
        services.AddScoped<RegionSeeder>();
        services.AddScoped<LocationSeeder>();

        return services;
    }
}
