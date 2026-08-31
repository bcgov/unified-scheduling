using Microsoft.EntityFrameworkCore;

namespace Unified.Common.Interceptors;

/// <summary>
/// Business rule that validates before database SaveChanges.
/// Runs inside the transaction - any exception causes rollback.
/// Rules are auto-discovered via DI (IEnumerable&lt;ISaveRule&gt;).
///
/// Re-entrancy: UnifiedDbContext inherits Audit.NET's AuditDbContext, which - after a successful
/// save - calls context.SaveChangesAsync() a second, nested time on the same DbContext to persist
/// the generated AuditRecord row. SaveRulesInterceptor skips this nested call outright (it only
/// ever adds an AuditRecord), so rules never see it. Rules stay safe regardless, as long as they
/// filter ChangeTracker entries by EntityState.Added/Modified (see "Common Mistakes" in
/// .github/skills/save-rule-pattern/SKILL.md) - by the time any nested save runs, entities from the
/// first save are already Unchanged, so a correctly-filtered rule finds nothing and no-ops.
/// </summary>
public interface ISaveRule
{
    /// <summary>
    /// Execute business rules before SaveChanges commits.
    /// Access entities via context.ChangeTracker.Entries&lt;T&gt;().
    /// Throw any exception to trigger rollback.
    /// </summary>
    /// <param name="context">DbContext with pending changes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ExecuteAsync(DbContext context, CancellationToken cancellationToken);
}
