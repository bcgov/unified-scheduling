using Microsoft.EntityFrameworkCore;
using Unified.Db.Models;
using Unified.Db.Models.Stats;
using Unified.Db.Models.Lookup;
using Unified.Db.Models.UserManagement;

namespace Unified.Db;

public class UnifiedDbContext : DbContext
{
    public UnifiedDbContext() { }

    public UnifiedDbContext(DbContextOptions<UnifiedDbContext> options)
        : base(options) { }

    public DbSet<Location> Locations { get; set; }
    public DbSet<Region> Regions { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<PositionType> PositionTypes { get; set; }

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
