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
        // Try to get connection string from environment variable (for CI/CD pipelines)
        // Falls back to local development default
        var connectionString = Environment.GetEnvironmentVariable("DatabaseConnectionString");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "DatabaseConnectionString is not set. Set it as an environment variable before running dotnet ef.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<UnifiedDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new UnifiedDbContext(optionsBuilder.Options);
    }
}
