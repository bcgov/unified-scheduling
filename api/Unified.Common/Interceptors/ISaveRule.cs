using Microsoft.EntityFrameworkCore;

namespace Unified.Common.Interceptors;

/// <summary>
/// Business rule that validates before database SaveChanges.
/// Runs inside the transaction - any exception causes rollback.
/// Rules are auto-discovered via DI (IEnumerable&lt;ISaveRule&gt;).
///
/// Re-entrancy: some interceptors (e.g. AuditRecordInterceptor) call context.SaveChangesAsync()
/// a second, nested time on the same DbContext to persist rows that depend on generated keys.
/// That nested call re-runs every ISaveRule. Rules stay safe automatically as long as they filter
/// ChangeTracker entries by EntityState.Added/Modified (see "Common Mistakes" in
/// .github/skills/save-rule-pattern/SKILL.md) - by the time the nested save runs, entities from the
/// first save are already Unchanged, so a correctly-filtered rule finds nothing and no-ops. If you
/// add a rule that reacts to Unchanged entities, has non-idempotent side effects, or does unconditional
/// work without an early exit, see that skill's "Nested SaveChangesAsync re-entrancy" section before
/// merging - you likely need an explicit suppression mechanism.
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
