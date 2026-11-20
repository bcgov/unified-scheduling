using Unified.Db;

namespace Unified.DataMigration.Services;

// Audit.NET's context flag does not affect EF interceptors or explicit AuditRecord inserts.
// Never use the process-wide Audit.Core.Configuration.AuditDisabled for migration work.
internal sealed class SsMigrationAuditScope : IDisposable
{
    private readonly UnifiedDbContext db;
    private readonly bool previous;

    internal SsMigrationAuditScope(UnifiedDbContext db, bool requireClean = false)
    {
        if (requireClean && db.ChangeTracker.HasChanges())
            throw new InvalidOperationException("SS migration requires a context without pending unrelated changes.");
        this.db = db;
        previous = db.AuditDisabled;
        db.AuditDisabled = true;
    }

    public void Dispose() => db.AuditDisabled = previous;
}
