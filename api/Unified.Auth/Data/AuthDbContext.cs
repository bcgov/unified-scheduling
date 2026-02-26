using Microsoft.EntityFrameworkCore;
using Unified.Auth.Data.Entities;

namespace Unified.Auth.Data;

public class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<UserEntity>();
        user.ToTable("users");

        user.HasKey(x => x.Id);

        user.Property(x => x.IdirName).HasMaxLength(200).IsRequired();
        user.Property(x => x.IsEnabled).IsRequired();
        user.Property(x => x.FirstName).HasMaxLength(150).IsRequired();
        user.Property(x => x.LastName).HasMaxLength(150).IsRequired();
        user.Property(x => x.Email).HasMaxLength(320).IsRequired();
        user.Property(x => x.HomeLocationId);
        user.Property(x => x.LastLogin);

        user.HasIndex(x => x.IdirName).IsUnique();
        user.HasIndex(x => x.Email).IsUnique();
    }
}
