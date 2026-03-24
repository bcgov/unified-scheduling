using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Unified.Db;

namespace Unified.Api.Services;

/// <summary>
/// Runs database migrations and registered seeders during application startup.
/// </summary>
public class MigrationAndSeedService(IServiceProvider services, ILogger<MigrationAndSeedService> logger)
{
    public IServiceProvider Services { get; } = services;
    private ILogger<MigrationAndSeedService> Logger { get; } = logger;

    public async Task ExecuteMigrationsAndSeeds()
    {
        try
        {
            using var scope = Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<UnifiedDbContext>();
            var seederFactory = scope.ServiceProvider.GetRequiredService<SeederFactory<UnifiedDbContext>>();

            await ExecuteMigrations(dbContext);
            seederFactory.LoadSeeders();
            await seederFactory.SeedAsync(dbContext);
        }
        catch (Exception ex)
        {
            Logger.LogCritical(ex, "Database migration failed on startup.");
            throw new InvalidOperationException("Database migration failed on startup.", ex);
        }
    }

    private async Task ExecuteMigrations(UnifiedDbContext dbContext)
    {
        Logger.LogInformation("Starting database migrations...");

        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
        var pendingList = pendingMigrations.ToList();

        Logger.LogInformation("Pending {Count} migrations.", pendingList.Count);
        if (pendingList.Count != 0)
        {
            Logger.LogDebug("{Migrations}", string.Join(", ", pendingList));
            await dbContext.Database.MigrateAsync();
            Logger.LogInformation("Migration(s) complete.");
        }
        else
        {
            Logger.LogInformation("Database is up to date.");
        }
    }
}
