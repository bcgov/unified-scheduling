using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Unified.Auth.Data;

/// <summary>
/// Design-time factory for creating AuthDbContext instances.
/// This is ONLY used by EF Core tools (dotnet ef migrations) and is never used at runtime.
/// At runtime, the DbContext is created via DI with connection strings from configuration.
/// </summary>
public class AuthDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AuthDbContext>();

        // Try to get connection string from environment variable (for CI/CD pipelines)
        // Falls back to local development default
        var connectionString = Environment.GetEnvironmentVariable("DatabaseConnectionString");

        optionsBuilder.UseNpgsql(connectionString);

        return new AuthDbContext(optionsBuilder.Options);
    }
}
