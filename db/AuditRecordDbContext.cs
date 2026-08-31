using Microsoft.EntityFrameworkCore;
using Unified.Db.Configuration;
using Unified.Db.Models;

namespace Unified.Db;

/// <summary>
/// Dedicated, minimal DbContext used only to persist <see cref="AuditRecord"/> rows.
/// Kept separate from <see cref="UnifiedDbContext"/> so that writing an audit record never
/// re-runs the interceptors registered on the main context (no recursive audit capture, no
/// re-entrant save-rule validation).
/// </summary>
public class AuditRecordDbContext(DbContextOptions<AuditRecordDbContext> options)
    : DbContext(options)
{
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AuditRecordConfiguration());
    }
}
