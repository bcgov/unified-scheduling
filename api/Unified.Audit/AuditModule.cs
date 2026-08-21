using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Unified.Audit.Interceptors;
using Unified.Common.Audit;

namespace Unified.Audit;

public static class AuditModule
{
    public static IServiceCollection AddAuditModule(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<AuditRecordInterceptorOptions>()
            .Bind(configuration.GetSection(AuditRecordInterceptorOptions.SectionName));

        services.AddScoped<ICurrentActorResolver, HttpContextActorResolver>();

        return services;
    }
}
