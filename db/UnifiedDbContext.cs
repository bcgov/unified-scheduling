using Audit.Core;
using Audit.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Unified.Db.Models;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Lookup;
using Unified.Db.Models.Scheduling;
using Unified.Db.Models.Stats;
using Unified.Db.Models.Training;
using Unified.Db.Models.UserManagement;

namespace Unified.Db;

/// <summary>
/// Inherits Audit.NET's <see cref="AuditDbContext"/> so every SaveChanges/SaveChangesAsync call is
/// automatically wrapped with audit capture (see Unified.Audit's README) - no EF Core
/// <c>IInterceptor</c> registration is needed for auditing.
/// </summary>
public class UnifiedDbContext : AuditDbContext
{
    private IDbContextTransaction? _auditTransaction;

    public UnifiedDbContext() { }

    public UnifiedDbContext(DbContextOptions<UnifiedDbContext> options)
        : base(options) { }

    public DbSet<AuditRecord> AuditRecords { get; set; }

    public DbSet<Location> Locations { get; set; }
    public DbSet<Region> Regions { get; set; }
    public DbSet<Permission> Permissions { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<RolePermission> RolePermissions { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<UserRole> UserRoles { get; set; }
    public DbSet<UserActingPosition> UserActingPositions { get; set; }
    public DbSet<UserAwayLocation> UserAwayLocations { get; set; }
    public DbSet<EventStatusType> EventStatusTypes { get; set; }
    public DbSet<EventType> EventTypes { get; set; }
    public DbSet<PositionType> PositionTypes { get; set; }
    public DbSet<CourtRoom> CourtRooms { get; set; }
    public DbSet<EventSeries> EventSeries { get; set; }
    public DbSet<Event> Events { get; set; }
    public DbSet<TrainingCategory> TrainingCategories { get; set; }
    public DbSet<Training> Trainings { get; set; }
    public DbSet<UserTraining> UserTrainings { get; set; }

    // Scheduling
    public DbSet<ShiftSeries> ShiftSeries { get; set; }
    public DbSet<ShiftSeriesUser> ShiftSeriesUsers { get; set; }
    public DbSet<ShiftEntry> ShiftEntries { get; set; }
    public DbSet<ShiftEntryUser> ShiftEntryUsers { get; set; }

    // Stats
    public DbSet<StatGroup> StatGroups { get; set; }
    public DbSet<StatCategory> StatCategories { get; set; }
    public DbSet<SubCategory> SubCategories { get; set; }
    public DbSet<StatMetric> StatMetrics { get; set; }
    public DbSet<SubCategoryMetric> SubCategoryMetrics { get; set; }
    public DbSet<StatRecord> StatRecords { get; set; }
    public DbSet<StatSignoff> StatSignoffs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyAllConfigurations(GetType().Assembly, this);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseNpgsql("Name=DatabaseConnectionString");
        }
    }

    // Opens before the entity save runs (Audit.NET calls this only when there are tracked changes
    // to audit), so the entity save and the AuditRecord insert it produces share one transaction.
    // Guarded by IsRelational() since non-relational providers (e.g. InMemory, used only in tests)
    // don't support transactions at all.
    public override void OnScopeCreated(IAuditScope auditScope)
    {
        if (Database.IsRelational() && Database.CurrentTransaction is null)
        {
            _auditTransaction = Database.BeginTransaction();
        }
    }

    // Called once the AuditRecord has been written (or attempted). Commits only on success so a
    // failed entity save or a failed audit insert rolls back both together; leaves an
    // already-active ambient transaction (opened by calling code) for its owner to resolve.
    public override void OnScopeSaved(IAuditScope auditScope)
    {
        if (_auditTransaction is null)
        {
            return;
        }

        if (auditScope.GetEntityFrameworkEvent()?.Success == true)
        {
            _auditTransaction.Commit();
        }
        else
        {
            _auditTransaction.Rollback();
        }

        _auditTransaction.Dispose();
        _auditTransaction = null;
    }

    // Safety net: if the AuditRecord insert itself throws, Audit.NET never calls OnScopeSaved (the
    // exception escapes from inside its scope-disposal), so the transaction opened in OnScopeCreated
    // would otherwise stay open. Guarantees rollback regardless of where the failure happened.
    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch
        {
            if (_auditTransaction is not null)
            {
                await _auditTransaction.RollbackAsync(cancellationToken);
                await _auditTransaction.DisposeAsync();
                _auditTransaction = null;
            }
            throw;
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        try
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        catch
        {
            if (_auditTransaction is not null)
            {
                _auditTransaction.Rollback();
                _auditTransaction.Dispose();
                _auditTransaction = null;
            }
            throw;
        }
    }
}
