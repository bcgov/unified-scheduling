using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Unified.Common.Interceptors;

namespace Unified.Api.Services;

/// <summary>
/// Registers EF Core interceptors in a single place to control registration order
/// without creating circular dependencies between domain modules.
/// </summary>
public static class InterceptorRegistration
{
    public static IServiceCollection AddInterceptors(this IServiceCollection services)
    {
        // Interceptors are executed by EF Core in the order they are registered.
        // 1. Business save rules run first to validate/modify domain state before audit recording.
        services.AddScoped<IInterceptor, SaveRulesInterceptor>();

        return services;
    }
}
