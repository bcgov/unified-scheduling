using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Unified.Db;

// Deliberately excludes AuditDbContext, business mappings, and business interceptors.
public sealed class DataProtectionKeyDbContext(DbContextOptions<DataProtectionKeyDbContext> options)
    : DbContext(options),
        IDataProtectionKeyContext
{
    public const string MigrationsHistoryTableName = "__DataProtectionMigrationsHistory";

    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }
}
