using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Unified.Db.Models;

namespace Unified.Db;

/// <summary>
/// Design-time factory for creating UnifiedDbContext instances.
/// This is ONLY used by EF Core tools (dotnet ef migrations) and is never used at runtime.
/// At runtime, the DbContext is created via DI with connection strings from configuration.
/// </summary>
public class UnifiedDbContextFactory : IDesignTimeDbContextFactory<UnifiedDbContext>
{
    public UnifiedDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<UnifiedDbContext>();

        // Try to get connection string from environment variable (for CI/CD pipelines)
        // Falls back to local development default
        var connectionString = Environment.GetEnvironmentVariable("DatabaseConnectionString");

        optionsBuilder.UseNpgsql(connectionString);

        return new UnifiedDbContext(optionsBuilder.Options);
    }
}
