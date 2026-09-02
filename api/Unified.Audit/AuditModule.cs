using Audit.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Unified.Audit.Options;
using Unified.Audit.Services;
using Unified.Audit.Validators;
using Unified.Authorization;
using Unified.Db;
using Unified.Db.Models;

namespace Unified.Audit;

public static class AuditModule
{
    public static IServiceCollection AddAuditModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuditRecordOptions>().Bind(configuration.GetSection(AuditRecordOptions.SectionName));

        services.AddHttpContextAccessor();
        // Singleton is safe: HttpContextActorResolver's only dependency, IHttpContextAccessor, is
        // itself Singleton and resolves the current request via AsyncLocal - UseAuditModule below
        // resolves this once at startup to configure the process-wide Audit.NET pipeline.
        services.AddSingleton<ICurrentActorResolver, HttpContextActorResolver>();

        services.AddMemoryCache();

        services.AddScoped<IAuditHistoryService, AuditHistoryService>();
        services.AddScoped<IAuditSchemaService, AuditSchemaService>();
        services.AddScoped<AuditHistoryQueryParamsValidator>();

        // Register permission policy owned by this module
        services.AddAuthorizationBuilder().AddPermissionPolicy(Permissions.AuditRead);

        return services;
    }

    /// <summary>
    /// Wires Audit.NET's built-in EntityFrameworkDataProvider (via <c>UseEntityFramework</c>) to map
    /// every audited entity to <see cref="AuditRecord"/>, populated by <see cref="AuditRecordEntityAction"/>.
    /// <see cref="Unified.Db.UnifiedDbContext"/> inherits Audit.NET's <c>AuditDbContext</c>, so
    /// SaveChanges/SaveChangesAsync are captured automatically - no EF Core <c>IInterceptor</c> is
    /// needed for auditing. Must be called once after the app's <see cref="IServiceProvider"/> is
    /// built (Audit.NET's configuration is a process-wide static).
    /// </summary>
    public static IServiceProvider UseAuditModule(this IServiceProvider services)
    {
        var currentActorResolver = services.GetRequiredService<ICurrentActorResolver>();
        var options = services.GetRequiredService<IOptions<AuditRecordOptions>>().Value;

        var entityAction = new AuditRecordEntityAction(currentActorResolver, options);

        // Populates AuditEvent.Activity from the ambient System.Diagnostics.Activity, which
        // AuditRecordEntityAction uses as the audit record's correlation id.
        global::Audit.Core.Configuration.IncludeActivityTrace = true;

        global::Audit
            .Core.Configuration.Setup()
            .UseEntityFramework(ef =>
                ef.AuditTypeMapper(_ => typeof(AuditRecord))
                    .AuditEntityAction<AuditRecord>(entityAction.Populate)
                    // Property names never match between the audited entity and AuditRecord (every
                    // entity type maps to the same table) - AuditRecordEntityAction sets every field.
                    .IgnoreMatchedProperties(true)
            );

        // Scoped to UnifiedDbContext (the only context this module audits): never audit writes to
        // the audit table itself.
        global::Audit
            .EntityFramework.Configuration.Setup()
            .ForContext<UnifiedDbContext>()
            .UseOptOut()
            .Ignore<AuditRecord>();

        return services;
    }
}
