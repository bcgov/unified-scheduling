using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Unified.Db;

public class DataProtectionKeyDbContextFactory
    : IDesignTimeDbContextFactory<DataProtectionKeyDbContext>
{
    public DataProtectionKeyDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DatabaseConnectionString");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "DatabaseConnectionString is not set. Set it as an environment variable before running dotnet ef."
            );

        var options = new DbContextOptionsBuilder<DataProtectionKeyDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql =>
                    npgsql.MigrationsHistoryTable(
                        DataProtectionKeyDbContext.MigrationsHistoryTableName
                    )
            )
            .Options;
        return new DataProtectionKeyDbContext(options);
    }
}
