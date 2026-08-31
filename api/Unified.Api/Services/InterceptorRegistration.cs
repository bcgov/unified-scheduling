using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Unified.Audit.Interceptors;
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
        // Interceptors are executed by EF Core in the order they are registered, for both the
        // "before save" and "after save" hooks.
        // 1. Business save rules run first to validate/modify domain state before audit recording.
        services.AddScoped<IInterceptor, SaveRulesInterceptor>();

        // 2. Audit.NET captures the pre/post-save entity snapshots and inserts audit records via
        //    the configured AuditRecordDataProvider (see AuditModule.UseAuditModule).
        services.AddScoped<IInterceptor, global::Audit.EntityFramework.AuditSaveChangesInterceptor>();

        // 3. Runs last so its transaction commit happens only after the audit record insert (step 2)
        //    has succeeded — see AuditTransactionInterceptor.
        services.AddScoped<IInterceptor, AuditTransactionInterceptor>();

        return services;
    }
}
