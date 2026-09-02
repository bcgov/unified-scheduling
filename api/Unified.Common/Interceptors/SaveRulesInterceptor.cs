using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Unified.Common.Interceptors;

/// <summary>
/// EF Core interceptor that runs all registered ISaveRules before SaveChanges.
/// Auto-discovers rules via DI container (IEnumerable&lt;ISaveRule&gt;).
/// Runs all rules inside the transaction - any exception causes rollback.
/// </summary>
public sealed class SaveRulesInterceptor(IEnumerable<ISaveRule> rules, ILogger<SaveRulesInterceptor> logger)
    : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        if (eventData.Context is null)
            return result;

        // The AuditRecord insert Audit.NET performs after a successful save re-runs this
        // interceptor (same DbContext, nested SaveChangesAsync call) - skip entirely when that's
        // the only pending change, since business rules never apply to the audit log itself.
        var hasNonAuditChanges = eventData
            .Context.ChangeTracker.Entries()
            .Any(entry =>
                entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                && entry.Entity.GetType().Name != "AuditRecord"
            );

        if (!hasNonAuditChanges)
            return result;

        // Run all rules before SaveChanges
        foreach (var rule in rules)
        {
            logger.LogDebug("Running rule: {RuleName}", rule.GetType().Name);
            try
            {
                await rule.ExecuteAsync(eventData.Context, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Rule {RuleName} failed: {Message}", rule.GetType().Name, ex.Message);
                throw; // Re-throw original exception to propagate error message and trigger rollback
            }
        }

        return result;
    }
}
