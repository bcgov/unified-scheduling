using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

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
        var connectionString =
            Environment.GetEnvironmentVariable("DatabaseConnectionString")
            ?? BuildDefaultConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "DatabaseConnectionString is not set. Set it as an environment variable before running dotnet ef."
            );
        }

        var optionsBuilder = new DbContextOptionsBuilder<UnifiedDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new UnifiedDbContext(optionsBuilder.Options);
    }

    private static string BuildDefaultConnectionString()
    {
        var host = IsRunningInContainer() ? "db" : "localhost";

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = 5432,
            Database = "unifieddb",
            Username = "uniuser",
            Password = "unipassword",
            Enlist = true,
            MinPoolSize = 10,
        };

        return builder.ConnectionString;
    }

    private static bool IsRunningInContainer() =>
        string.Equals(
            Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
            "true",
            StringComparison.OrdinalIgnoreCase
        );
}
