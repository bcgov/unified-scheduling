using Microsoft.EntityFrameworkCore;
using Npgsql;
using Unified.Db;
using Unified.Db.Extensions;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Interceptors;

namespace Unified.Tests.Scheduling.Services;

public sealed class PostgresConcurrencyTests
{
    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UNIFIED_TEST_POSTGRES_CONNECTION_STRING"));

    [Theory(
        Skip = "Set UNIFIED_TEST_POSTGRES_CONNECTION_STRING to run against PostgreSQL.",
        SkipUnless = nameof(IsConfigured)
    )]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveChanges_WithPostgresXmin_AutomaticallyRejectsStaleWriterAndSupportsRepeatedSaves(
        bool asyncSave
    )
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new NpgsqlConnection(
            Environment.GetEnvironmentVariable("UNIFIED_TEST_POSTGRES_CONNECTION_STRING")
        );
        await connection.OpenAsync(token);
        var options = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseNpgsql(connection)
            .AddInterceptors(new SchedulingConcurrencyInterceptor())
            .Options;
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

        await using (var transaction = await first.Database.BeginTransactionAsync(token))
        {
            // xmin is a transaction ID, so multiple updates in one transaction share it.
            for (var save = 0; save < 2; save++)
            {
                current.Users.Add(new ShiftSeriesUser { UserId = Guid.NewGuid() });
                if (asyncSave)
                    await first.SaveChangesAsync(token);
                else
                    first.SaveChanges();
                Assert.NotEqual(originalVersion, current.ConcurrencyToken);
                Assert.Equal(
                    current.ConcurrencyToken,
                    first.Entry(current).Property(item => item.ConcurrencyToken).OriginalValue
                );
            }
            await transaction.CommitAsync(token);
        }

        second.RequireVersion(stale, originalVersion);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(token));
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

    [Theory(
        Skip = "Set UNIFIED_TEST_POSTGRES_CONNECTION_STRING to run against PostgreSQL.",
        SkipUnless = nameof(IsConfigured)
    )]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CalendarChanges_WithPostgresXmin_AdvanceOnlyChangedSchedulingOccurrences(
        bool assignment,
        bool propagation
    )
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new NpgsqlConnection(
            Environment.GetEnvironmentVariable("UNIFIED_TEST_POSTGRES_CONNECTION_STRING")
        );
        await connection.OpenAsync(ct);
        var options = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseNpgsql(connection)
            .AddInterceptors(new SchedulingConcurrencyInterceptor())
            .Options;
        await using var db = new CalendarTestDb(options);
        await db.Database.ExecuteSqlRawAsync(
            db.Database.GenerateCreateScript().Replace("CREATE TABLE", "CREATE TEMP TABLE"),
            ct
        );
        var eventType = assignment ? "assignment" : "shift";
        var eventSeries = new EventSeries { EventTypeCode = eventType, Title = "Series" };
        var occurrence = new Event
        {
            SourceModule = "scheduling",
            EventTypeCode = eventType,
            EventSeries = eventSeries,
        };
        var overridden = new Event
        {
            SourceModule = "scheduling",
            EventTypeCode = eventType,
            EventSeries = eventSeries,
            Title = "Override",
        };
        BaseEntity root,
            unaffected,
            series;
        if (assignment)
        {
            var parent = new AssignmentSeries { EventSeries = eventSeries };
            series = parent;
            root = new AssignmentEntry { AssignmentSeries = parent, Event = occurrence };
            unaffected = new AssignmentEntry { AssignmentSeries = parent, Event = overridden };
        }
        else
        {
            var parent = new ShiftSeries { EventSeries = eventSeries };
            series = parent;
            root = new ShiftEntry { ShiftSeries = parent, Event = occurrence };
            unaffected = new ShiftEntry { ShiftSeries = parent, Event = overridden };
        }
        db.AddRange(root, unaffected);
        await db.SaveChangesAsync(ct);
        var rootVersion = root.ConcurrencyToken;
        var seriesVersion = series.ConcurrencyToken;
        var unaffectedVersion = unaffected.ConcurrencyToken;

        if (propagation)
            eventSeries.Title = "Updated series";
        occurrence.Title = "Updated occurrence";
        await db.SaveChangesAsync(ct);
        Assert.NotEqual(rootVersion, root.ConcurrencyToken);
        Assert.NotEqual(seriesVersion, series.ConcurrencyToken);
        Assert.Equal(unaffectedVersion, unaffected.ConcurrencyToken);

        rootVersion = root.ConcurrencyToken;
        seriesVersion = series.ConcurrencyToken;
        eventSeries.Notes = "Series-only edit";
        await db.SaveChangesAsync(ct);
        Assert.Equal(rootVersion, root.ConcurrencyToken);
        Assert.NotEqual(seriesVersion, series.ConcurrencyToken);
        Assert.Equal(unaffectedVersion, unaffected.ConcurrencyToken);

        var unrelated = new Event { SourceModule = "training", EventTypeCode = "training" };
        db.Add(unrelated);
        await db.SaveChangesAsync(ct);
        rootVersion = root.ConcurrencyToken;
        seriesVersion = series.ConcurrencyToken;
        unrelated.Title = "Updated training";
        await db.SaveChangesAsync(ct);
        Assert.Equal(rootVersion, root.ConcurrencyToken);
        Assert.Equal(seriesVersion, series.ConcurrencyToken);
        Assert.Equal(unaffectedVersion, unaffected.ConcurrencyToken);
    }

    [Theory(
        Skip = "Set UNIFIED_TEST_POSTGRES_CONNECTION_STRING to run against PostgreSQL.",
        SkipUnless = nameof(IsConfigured)
    )]
    [InlineData(false, "add")]
    [InlineData(false, "update")]
    [InlineData(false, "remove")]
    [InlineData(false, "unchanged")]
    [InlineData(false, "saved-root")]
    [InlineData(true, "add")]
    [InlineData(true, "update")]
    [InlineData(true, "remove")]
    [InlineData(true, "suppress")]
    [InlineData(true, "unchanged")]
    public async Task RelationshipChanges_WithPostgresXmin_InvalidateAllAffectedRoots(bool series, string mutation)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new NpgsqlConnection(
            Environment.GetEnvironmentVariable("UNIFIED_TEST_POSTGRES_CONNECTION_STRING")
        );
        await connection.OpenAsync(ct);
        var options = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseNpgsql(connection)
            .AddInterceptors(new SchedulingConcurrencyInterceptor())
            .Options;
        await using var writer = new RelationshipTestDb(options);
        await writer.Database.ExecuteSqlRawAsync(
            writer.Database.GenerateCreateScript().Replace("CREATE TABLE", "CREATE TEMP TABLE"),
            ct
        );
        var shiftSeries = new ShiftSeries();
        var assignmentSeries = new AssignmentSeries();
        var shift = new ShiftEntry { ShiftSeries = shiftSeries };
        var assignment = new AssignmentEntry { AssignmentSeries = assignmentSeries };
        writer.AddRange(shift, assignment);
        await writer.SaveChangesAsync(ct);
        var userId = Guid.NewGuid();
        var parent = new ShiftAssignmentSeriesLink
        {
            ShiftSeries = shiftSeries,
            AssignmentSeries = assignmentSeries,
            Users = [new ShiftAssignmentSeriesLinkUser { UserId = userId }],
        };
        var link = new ShiftAssignmentEntry
        {
            ShiftEntry = shift,
            AssignmentEntry = assignment,
            Users = [new ShiftAssignmentEntryUser { UserId = userId }],
            ShiftAssignmentSeriesLink = series ? parent : null,
        };
        if (mutation is not ("add" or "saved-root"))
        {
            writer.Add(link);
            await writer.SaveChangesAsync(ct);
        }
        await using var staleWriter = new RelationshipTestDb(options);
        BaseEntity[] stale =
        [
            await staleWriter.ShiftSeries.SingleAsync(ct),
            await staleWriter.AssignmentSeries.SingleAsync(ct),
            await staleWriter.ShiftEntries.SingleAsync(ct),
            await staleWriter.AssignmentEntries.SingleAsync(ct),
        ];
        BaseEntity[] current = [shiftSeries, assignmentSeries, shift, assignment];
        await using (var transaction = await writer.Database.BeginTransactionAsync(ct))
        {
            if (mutation == "saved-root")
            {
                writer.RequireVersion(shift, shift.ConcurrencyToken);
                await writer.SaveChangesAsync(ct);
            }
            if (mutation is "add" or "saved-root")
                writer.Add(link);
            if (mutation == "update")
            {
                link.Users.Single().UserId = Guid.NewGuid();
                if (series)
                    parent.Users.Single().UserId = Guid.NewGuid();
            }
            if (mutation == "remove")
            {
                writer.Remove(link);
                if (series)
                    writer.Remove(parent);
            }
            if (mutation == "suppress")
            {
                writer.ShiftAssignmentEntryUsers.RemoveRange(link.Users);
                link.IsException = true;
            }
            await writer.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        for (var index = 0; index < stale.Length; index++)
        {
            if (mutation == "unchanged")
            {
                Assert.Equal(stale[index].ConcurrencyToken, current[index].ConcurrencyToken);
                continue;
            }
            Assert.NotEqual(stale[index].ConcurrencyToken, current[index].ConcurrencyToken);
            staleWriter.ChangeTracker.Clear();
            staleWriter.Entry(stale[index]).State = EntityState.Unchanged;
            staleWriter.RequireVersion(stale[index], stale[index].ConcurrencyToken);
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => staleWriter.SaveChangesAsync(ct));
        }
    }

    private sealed class RelationshipTestDb(DbContextOptions<UnifiedDbContext> options) : UnifiedDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            Type[] types =
            [
                typeof(ShiftSeries),
                typeof(AssignmentSeries),
                typeof(ShiftEntry),
                typeof(AssignmentEntry),
                typeof(ShiftAssignmentEntry),
                typeof(ShiftAssignmentEntryUser),
                typeof(ShiftAssignmentSeriesLink),
                typeof(ShiftAssignmentSeriesLinkUser),
            ];
            foreach (var type in modelBuilder.Model.GetEntityTypes().ToArray())
                if (!types.Contains(type.ClrType))
                    modelBuilder.Ignore(type.ClrType);
            modelBuilder.Entity<ShiftSeries>().Ignore(item => item.EventSeries).Ignore(item => item.Users);
            modelBuilder
                .Entity<AssignmentSeries>()
                .Ignore(item => item.EventSeries)
                .Ignore(item => item.AssignmentDefinition)
                .Ignore(item => item.Category)
                .Ignore(item => item.SubCategory);
            modelBuilder.Entity<ShiftEntry>().Ignore(item => item.Event).Ignore(item => item.Users);
            modelBuilder
                .Entity<AssignmentEntry>()
                .Ignore(item => item.Event)
                .Ignore(item => item.AssignmentDefinition)
                .Ignore(item => item.Category)
                .Ignore(item => item.SubCategory);
        }
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

    private sealed class CalendarTestDb(DbContextOptions<UnifiedDbContext> options) : UnifiedDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            Type[] types =
            [
                typeof(ShiftSeries),
                typeof(AssignmentSeries),
                typeof(ShiftEntry),
                typeof(AssignmentEntry),
                typeof(Event),
                typeof(EventSeries),
            ];
            foreach (var type in modelBuilder.Model.GetEntityTypes().ToArray())
                if (!types.Contains(type.ClrType))
                    modelBuilder.Ignore(type.ClrType);
            modelBuilder
                .Entity<ShiftSeries>()
                .Ignore(item => item.Users)
                .Ignore(item => item.ShiftAssignmentSeriesLinks);
            modelBuilder.Entity<ShiftEntry>().Ignore(item => item.Users).Ignore(item => item.ShiftAssignmentEntries);
            modelBuilder
                .Entity<AssignmentSeries>()
                .Ignore(item => item.ShiftAssignmentSeriesLinks)
                .Ignore(item => item.AssignmentDefinition)
                .Ignore(item => item.Category)
                .Ignore(item => item.SubCategory);
            modelBuilder
                .Entity<AssignmentEntry>()
                .Ignore(item => item.ShiftAssignmentEntries)
                .Ignore(item => item.AssignmentDefinition)
                .Ignore(item => item.Category)
                .Ignore(item => item.SubCategory);
        }
    }
}
