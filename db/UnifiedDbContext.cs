using Microsoft.EntityFrameworkCore;
using Unified.Db.Models.UserManagement;

namespace Unified.Db;

public class UnifiedDbContext : DbContext
{
    public UnifiedDbContext() { }

    public UnifiedDbContext(DbContextOptions<UnifiedDbContext> options)
        : base(options) { }

    public DbSet<User> Users { get; set; }

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
