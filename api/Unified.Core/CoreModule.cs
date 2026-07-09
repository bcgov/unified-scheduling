using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Unified.Core.Seeders;
using Unified.Core.Services;
using Unified.Core.Services.Lookup;

namespace Unified.Core;

public static class CoreModule
{
    public static IServiceCollection AddCoreModule(
        this IServiceCollection services,
        bool calendarModuleEnabled,
        bool schedulingModuleEnabled
    )
    {
        services.AddScoped<ILookupService, LookupService>();
        services.AddScoped<ILookupStrategy, PositionTypeLookupStrategy>();
        if (calendarModuleEnabled)
        {
            services.AddScoped<ILookupStrategy, EventTypeLookupStrategy>();
            services.AddScoped<ILookupStrategy, EventStatusTypeLookupStrategy>();
        }
        if (schedulingModuleEnabled)
        {
            services.AddScoped<ILookupStrategy, AssignmentTypeLookupStrategy>();
        }
        services.AddScoped<PositionTypeSeeder>();

        return services;
    }
}
