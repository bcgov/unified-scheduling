using Microsoft.EntityFrameworkCore;
using Npgsql;
using Unified.Db;
using Unified.Db.Extensions;
using Unified.Db.Models.Scheduling;

namespace Unified.Tests.Scheduling.Services;

public sealed class PostgresConcurrencyTests
{
    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UNIFIED_TEST_POSTGRES_CONNECTION_STRING"));

    [Fact(
        Skip = "Set UNIFIED_TEST_POSTGRES_CONNECTION_STRING to run against PostgreSQL.",
        SkipUnless = nameof(IsConfigured)
    )]
    public async Task RequireVersion_WithPostgresXmin_RejectsStaleWriterAndAdvancesRootForRelationshipEdit()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new NpgsqlConnection(
            Environment.GetEnvironmentVariable("UNIFIED_TEST_POSTGRES_CONNECTION_STRING")
        );
        await connection.OpenAsync(token);
        var options = new DbContextOptionsBuilder<UnifiedDbContext>().UseNpgsql(connection).Options;
        await using var first = new PostgresTestDb(options);
        // Temporary tables live only on this connection. Two independent change trackers
        // share it sequentially; no application tables, schema or migrations are changed.
        await first.Database.ExecuteSqlRawAsync(
            first.Database.GenerateCreateScript().Replace("CREATE TABLE", "CREATE TEMP TABLE"),
            token
        );
        var root = new ShiftSeries();
        first.Add(root);
        await first.SaveChangesAsync(token);
        first.ChangeTracker.Clear();

        var current = await first.ShiftSeries.SingleAsync(token);
        await using var second = new PostgresTestDb(options);
        var stale = await second.ShiftSeries.SingleAsync(token);
        var originalVersion = stale.ConcurrencyToken;
        Assert.Equal(originalVersion, current.ConcurrencyToken);

        // Only a relationship changes; RequireVersion makes the unchanged root participate.
        current.Users.Add(new ShiftSeriesUser { UserId = Guid.NewGuid() });
        first.RequireVersion(current, originalVersion);
        await first.SaveChangesAsync(token);
        var updatedVersion = current.ConcurrencyToken;
        Assert.NotEqual(originalVersion, updatedVersion);

        second.RequireVersion(stale, originalVersion);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(token));

        // Reuse the store-returned version for another logical edit and save.
        current.Users.Add(new ShiftSeriesUser { UserId = Guid.NewGuid() });
        first.RequireVersion(current, updatedVersion);
        await first.SaveChangesAsync(token);
        Assert.NotEqual(updatedVersion, current.ConcurrencyToken);
        Assert.Equal(
            current.ConcurrencyToken,
            first.Entry(current).Property(item => item.ConcurrencyToken).OriginalValue
        );
        Assert.Equal(
            current.ConcurrencyToken,
            (await second.ShiftSeries.AsNoTracking().SingleAsync(token)).ConcurrencyToken
        );
        Assert.Equal(2, await first.ShiftSeriesUsers.CountAsync(token));
    }

    private sealed class PostgresTestDb(DbContextOptions<UnifiedDbContext> options) : UnifiedDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Keep the real scheduling entities and inherited xmin mapping; omit unrelated
            // navigation graphs so this test needs only two connection-local temporary tables.
            foreach (var type in modelBuilder.Model.GetEntityTypes().ToArray())
                if (type.ClrType != typeof(ShiftSeries) && type.ClrType != typeof(ShiftSeriesUser))
                    modelBuilder.Ignore(type.ClrType);

            modelBuilder.Entity<ShiftSeries>().Ignore(item => item.EventSeries).Ignore(item => item.ShiftEntries);
            modelBuilder.Entity<ShiftSeries>().Ignore(item => item.ShiftAssignmentSeriesLinks);
            modelBuilder
                .Entity<ShiftSeries>()
                .HasMany(item => item.Users)
                .WithOne(item => item.ShiftSeries)
                .HasForeignKey(item => item.ShiftSeriesId);
        }
    }
}
