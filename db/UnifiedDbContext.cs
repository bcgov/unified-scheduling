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
    public DbSet<AssignmentSeries> AssignmentSeries { get; set; }
    public DbSet<AssignmentDefinition> AssignmentDefinitions { get; set; }
    public DbSet<AssignmentEntry> AssignmentEntries { get; set; }
    public DbSet<ShiftAssignmentSeriesLink> ShiftAssignmentSeriesLinks { get; set; }
    public DbSet<ShiftAssignmentSeriesLinkUser> ShiftAssignmentSeriesLinkUsers { get; set; }
    public DbSet<ShiftAssignmentEntry> ShiftAssignmentEntries { get; set; }
    public DbSet<ShiftAssignmentEntryUser> ShiftAssignmentEntryUsers { get; set; }

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

    private const string SavepointName = "UnifiedDbContext_SaveChanges";

    // Shares one transaction across the entity save and the nested AuditRecord insert Audit.NET
    // performs on success, so a failure in either rolls back both together. Guarded by
    // IsRelational() since the in-memory provider (used only in tests) doesn't support
    // transactions at all. An already-active ambient transaction (opened by calling code) is
    // wrapped in a savepoint instead, so this save is still rolled back on failure even if the
    // owner of that transaction catches the exception and keeps going.
    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default
    )
    {
        if (!Database.IsRelational())
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        if (Database.CurrentTransaction is { } ambientTransaction)
        {
            await ambientTransaction.CreateSavepointAsync(SavepointName, cancellationToken);

            try
            {
                var result = await base.SaveChangesAsync(
                    acceptAllChangesOnSuccess,
                    cancellationToken
                );

                await ambientTransaction.ReleaseSavepointAsync(SavepointName, cancellationToken);

                return result;
            }
            catch
            {
                await ambientTransaction.RollbackToSavepointAsync(SavepointName, cancellationToken);

                throw;
            }
        }

        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);

        var ownedResult = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return ownedResult;
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        if (!Database.IsRelational())
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        if (Database.CurrentTransaction is { } ambientTransaction)
        {
            ambientTransaction.CreateSavepoint(SavepointName);

            try
            {
                var result = base.SaveChanges(acceptAllChangesOnSuccess);

                ambientTransaction.ReleaseSavepoint(SavepointName);

                return result;
            }
            catch
            {
                ambientTransaction.RollbackToSavepoint(SavepointName);

                throw;
            }
        }

        using var transaction = Database.BeginTransaction();

        var ownedResult = base.SaveChanges(acceptAllChangesOnSuccess);

        transaction.Commit();

        return ownedResult;
    }
}
