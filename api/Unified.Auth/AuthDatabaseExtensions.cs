using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Unified.Auth.Data;

namespace Unified.Auth;

/// <summary>
/// Database migration extensions for the Auth module
/// </summary>
public static class AuthDatabaseExtensions
{
    /// <summary>
    /// Apply pending database migrations for the Auth module
    /// </summary>
    /// <param name="app">Application builder</param>
    /// <returns>Application builder for chaining</returns>
    public static async Task<WebApplication> MigrateAuthDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AuthDbContext>>();

        try
        {
            logger.LogInformation("Checking for pending Auth database migrations...");

            var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
            var pendingList = pendingMigrations.ToList();

            if (pendingList.Any())
            {
                logger.LogInformation(
                    "Applying {Count} pending migrations: {Migrations}",
                    pendingList.Count,
                    string.Join(", ", pendingList)
                );

                await dbContext.Database.MigrateAsync();

                logger.LogInformation("Auth database migrations applied successfully");
            }
            else
            {
                logger.LogInformation("Auth database is up to date");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while migrating the Auth database");
            throw;
        }

        return app;
    }
}
