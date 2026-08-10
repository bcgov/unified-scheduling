using Microsoft.EntityFrameworkCore;

namespace Unified.Common.Interceptors;

/// <summary>
/// Business rule that validates before database SaveChanges.
/// Runs inside the transaction - any exception causes rollback.
/// Rules are auto-discovered via DI (IEnumerable&lt;ISaveRule&gt;).
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
