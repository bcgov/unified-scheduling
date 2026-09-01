using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Unified.Common.Interceptors;

namespace Unified.Api.Services;

/// <summary>
/// Registers EF Core interceptors. Auditing itself needs no entry here - <c>UnifiedDbContext</c>
/// inherits Audit.NET's <c>AuditDbContext</c>, which wraps SaveChanges/SaveChangesAsync directly
/// (see Unified.Audit's README).
/// </summary>
public static class InterceptorRegistration
{
    public static IServiceCollection AddInterceptors(this IServiceCollection services)
    {
        // Interceptors are executed by EF Core in the order they are registered.
        // 1. Business save rules
        services.AddScoped<IInterceptor, SaveRulesInterceptor>();

        return services;
    }
}

