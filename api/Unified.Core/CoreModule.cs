using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Unified.Common.Seeding;
using Unified.Core.Seeders;
using Unified.Core.Services;
using Unified.Core.Services.Lookup;
using Unified.Db;

namespace Unified.Core;

public static class CoreModule
{
    public static IServiceCollection AddCoreModule(this IServiceCollection services)
    {
        services.AddScoped<ILookupService, LookupService>();

        services.AddScoped<ILookupStrategy, PositionTypeLookupStrategy>();
        services.AddScoped<ILookupStrategy, EventTypeLookupStrategy>();
        services.AddScoped<ILookupStrategy, EventStatusTypeLookupStrategy>();

        services.AddSeeder<UnifiedDbContext, PositionTypeSeeder>();

        return services;
    }
}
