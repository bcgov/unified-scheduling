using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Unified.Audit.Interceptors;
using Unified.Audit.Services;
using Unified.Audit.Validators;
using Unified.Authorization;
using Unified.Common.Audit;

namespace Unified.Audit;

public static class AuditModule
{
    public static IServiceCollection AddAuditModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuditRecordOptions>().Bind(configuration.GetSection(AuditRecordOptions.SectionName));

        services.AddScoped<ICurrentActorResolver, HttpContextActorResolver>();

        services.AddMemoryCache();
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IAuditHistoryService, AuditHistoryService>();
        services.AddScoped<IAuditSchemaService, AuditSchemaService>();
        services.AddScoped<AuditHistoryQueryParamsValidator>();

        // Register permission policy owned by this module
        services.AddAuthorizationBuilder().AddPermissionPolicy(Permissions.AuditRead);

        return services;
    }
}
