using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Unified.Audit.Interceptors;
using Unified.Audit.Services;
using Unified.Audit.Validators;
using Unified.Authorization;
using Unified.Common.Audit;
using Unified.Db;
using Unified.Db.Models;

namespace Unified.Audit;

public static class AuditModule
{
    public static IServiceCollection AddAuditModule(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<AuditRecordInterceptorOptions>()
            .Bind(configuration.GetSection(AuditRecordInterceptorOptions.SectionName));

        services.AddHttpContextAccessor();
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

    /// <summary>
    /// Wires the Audit.NET global configuration to the module's <see cref="AuditRecordDataProvider"/>.
    /// Must be called once after the app's <see cref="IServiceProvider"/> is built (Audit.NET's
    /// configuration is a process-wide static).
    /// </summary>
    public static IServiceProvider UseAuditModule(this IServiceProvider services)
    {
        var httpContextAccessor = services.GetRequiredService<IHttpContextAccessor>();
        var options = services.GetRequiredService<IOptions<AuditRecordInterceptorOptions>>();

        // Populates AuditEvent.Activity from the ambient System.Diagnostics.Activity, giving
        // AuditRecordDataProvider a correlation id fallback for non-HTTP contexts (e.g. Hangfire jobs).
        global::Audit.Core.Configuration.IncludeActivityTrace = true;

        global::Audit.Core.Configuration.DataProvider = new AuditRecordDataProvider(
            connection => new DbContextOptionsBuilder<AuditRecordDbContext>().UseNpgsql(connection).Options,
            new HttpContextActorResolver(httpContextAccessor),
            httpContextAccessor,
            options
        );

        // Applies to any audited DbContext (no context-specific config overrides it): never audit
        // writes to the audit table itself.
        global::Audit.EntityFramework.Configuration.Setup().ForAnyContext().UseOptOut().Ignore<AuditRecord>();

        return services;
    }
}
