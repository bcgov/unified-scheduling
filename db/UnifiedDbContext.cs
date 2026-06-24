using Microsoft.EntityFrameworkCore;
using Unified.Db.Models;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Lookup;
using Unified.Db.Models.Stats;
using Unified.Db.Models.Training;
using Unified.Db.Models.UserManagement;

namespace Unified.Db;

public class UnifiedDbContext : DbContext
{
    public UnifiedDbContext() { }

    public UnifiedDbContext(DbContextOptions<UnifiedDbContext> options)
        : base(options) { }

    public DbSet<Location> Locations { get; set; }
    public DbSet<Region> Regions { get; set; }
    public DbSet<Permission> Permissions { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<RolePermission> RolePermissions { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<UserRole> UserRoles { get; set; }
    public DbSet<UserActingPosition> UserActingPositions { get; set; }
    public DbSet<EventStatusType> EventStatusTypes { get; set; }
    public DbSet<EventType> EventTypes { get; set; }
    public DbSet<PositionType> PositionTypes { get; set; }
    public DbSet<EventSeries> EventSeries { get; set; }
    public DbSet<Event> Events { get; set; }
    public DbSet<TrainingCategory> TrainingCategories { get; set; }
    public DbSet<Training> Trainings { get; set; }
    public DbSet<UserTraining> UserTrainings { get; set; }

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
}
