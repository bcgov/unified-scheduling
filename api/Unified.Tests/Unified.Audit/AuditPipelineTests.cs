using Audit.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Unified.Audit;
using Unified.Audit.Options;
using Unified.Db;
using Unified.Db.Models;
using Unified.Tests.TestHelpers;

namespace Unified.Tests.Unified.Audit;

/// <summary>
/// Exercises the real Audit.NET pipeline end-to-end against a relational (SQLite) <see cref="UnifiedDbContext"/> -
/// the in-memory provider used by other Audit tests doesn't support transactions, so it can't verify the
/// rollback guarantees covered here. <c>Audit.Core.Configuration</c> is a process-wide static (disabled by
/// default for the whole assembly by <see cref="ModuleInitialization"/>); each test re-enables and
/// re-disables it around its own body via <see cref="InitializeAsync"/>/<see cref="DisposeAsync"/>.
/// </summary>
public sealed class AuditPipelineTests : IAsyncLifetime
{
    private readonly Guid _actorId = Guid.NewGuid();
    private const string ActorName = "Jane Doe";

    public ValueTask InitializeAsync()
    {
        global::Audit.Core.Configuration.AuditDisabled = false;
        global::Audit.Core.Configuration.IncludeActivityTrace = true;

        var entityAction = new AuditRecordEntityAction(
            new FakeCurrentActorResolver(_actorId, ActorName),
            new AuditRecordOptions()
        );

        global::Audit.Core.Configuration
            .Setup()
            .UseEntityFramework(ef =>
                ef.AuditTypeMapper(_ => typeof(AuditRecord))
                    .AuditEntityAction<AuditRecord>(entityAction.Populate)
                    .IgnoreMatchedProperties(true)
            );

        global::Audit.EntityFramework.Configuration.Setup().ForAnyContext().UseOptOut().Ignore<AuditRecord>();

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        global::Audit.Core.Configuration.AuditDisabled = true;
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task SaveChangesAsync_When_Entity_Added_Should_Write_AuditRecord_With_Generated_EntityPK()
    {
        var (connection, dbContext) = await CreateSqliteDbContextAsync();
        await using var _ = connection;
        await using var __ = dbContext;

        var region = new Region { Name = "South" };
        dbContext.Regions.Add(region);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var record = Assert.Single(await dbContext.AuditRecords.ToListAsync(TestContext.Current.CancellationToken));

        Assert.True(region.Id > 0);
        Assert.Equal("Added", record.Action);
        Assert.Equal(nameof(Region), record.EntityType);
        Assert.Equal("Regions", record.TableName);
        Assert.Equal(region.Id.ToString(), record.EntityPK);
        Assert.Equal(_actorId, record.ActorUserId);
        Assert.Equal(ActorName, record.ActorName);
        Assert.Null(record.OldValues);
        Assert.Contains("South", record.NewValues);
    }

    [Fact]
    public async Task SaveChangesAsync_When_Entity_Updated_Should_Write_AuditRecord_With_Changed_Columns()
    {
        var (connection, dbContext) = await CreateSqliteDbContextAsync();
        await using var _ = connection;
        await using var __ = dbContext;

        var region = new Region { Name = "South" };
        dbContext.Regions.Add(region);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        region.Name = "North";
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var record = Assert.Single(
            await dbContext
                .AuditRecords.Where(r => r.Action == "Modified")
                .ToListAsync(TestContext.Current.CancellationToken)
        );

        Assert.Equal(region.Id.ToString(), record.EntityPK);
        Assert.Contains("Name", record.ChangedColumns!);
        Assert.Contains("South", record.OldValues);
        Assert.Contains("North", record.NewValues);
    }

    [Fact]
    public async Task SaveChangesAsync_When_Entity_Deleted_Should_Write_AuditRecord_With_Old_Values()
    {
        var (connection, dbContext) = await CreateSqliteDbContextAsync();
        await using var _ = connection;
        await using var __ = dbContext;

        var region = new Region { Name = "South" };
        dbContext.Regions.Add(region);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var regionId = region.Id;

        dbContext.Regions.Remove(region);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var record = Assert.Single(
            await dbContext
                .AuditRecords.Where(r => r.Action == "Deleted")
                .ToListAsync(TestContext.Current.CancellationToken)
        );

        Assert.Equal(regionId.ToString(), record.EntityPK);
        Assert.Contains("South", record.OldValues);
        Assert.Null(record.NewValues);
    }

    [Fact]
    public async Task SaveChangesAsync_When_Saving_Parent_And_Child_Together_Should_Record_Correct_Generated_Ids()
    {
        var (connection, dbContext) = await CreateSqliteDbContextAsync();
        await using var _ = connection;
        await using var __ = dbContext;

        var region = new Region { Name = "North" };
        var location = new Location
        {
            AgencyId = "AG1",
            Name = "Loc 1",
            Timezone = "America/Vancouver",
            Region = region,
        };
        dbContext.Regions.Add(region);
        dbContext.Locations.Add(location);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.True(region.Id > 0);
        Assert.True(location.Id > 0);
        Assert.Equal(region.Id, location.RegionId);

        // Re-read untracked to confirm the FK was actually persisted, not just fixed up in memory.
        var persistedLocation = await dbContext
            .Locations.AsNoTracking()
            .SingleAsync(l => l.Id == location.Id, TestContext.Current.CancellationToken);
        Assert.Equal(region.Id, persistedLocation.RegionId);

        var records = await dbContext.AuditRecords.ToListAsync(TestContext.Current.CancellationToken);
        var regionRecord = Assert.Single(records, r => r.EntityType == nameof(Region));
        var locationRecord = Assert.Single(records, r => r.EntityType == nameof(Location));

        Assert.Equal(region.Id.ToString(), regionRecord.EntityPK);
        Assert.Equal(location.Id.ToString(), locationRecord.EntityPK);
        // Confirms the FK fix-up (RegionId) was captured in the audited NewValues, not just the in-memory entity.
        Assert.Contains($"\"RegionId\":{region.Id}", locationRecord.NewValues);
    }

    [Fact]
    public async Task SaveChangesAsync_When_Audit_Insert_Fails_Should_Roll_Back_Entity_Save_Too()
    {
        var (connection, dbContext) = await CreateSqliteDbContextAsync(
            new SimulatedDbFailureInterceptor(
                Options.Create(new SimulatedDbFailureOptions { Enabled = true, TableName = "AuditRecords" })
            )
        );
        await using var _ = connection;
        await using var __ = dbContext;

        dbContext.Regions.Add(new Region { Name = "South" });

        await Assert.ThrowsAnyAsync<Exception>(() =>
            dbContext.SaveChangesAsync(TestContext.Current.CancellationToken)
        );

        Assert.Empty(await dbContext.Regions.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await dbContext.AuditRecords.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveChangesAsync_When_Entity_Insert_Fails_Should_Not_Write_AuditRecord()
    {
        var (connection, dbContext) = await CreateSqliteDbContextAsync(
            new SimulatedDbFailureInterceptor(
                Options.Create(new SimulatedDbFailureOptions { Enabled = true, TableName = "Regions" })
            )
        );
        await using var _ = connection;
        await using var __ = dbContext;

        dbContext.Regions.Add(new Region { Name = "South" });

        await Assert.ThrowsAnyAsync<Exception>(() =>
            dbContext.SaveChangesAsync(TestContext.Current.CancellationToken)
        );

        Assert.Empty(await dbContext.Regions.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await dbContext.AuditRecords.ToListAsync(TestContext.Current.CancellationToken));
    }

    private static async Task<(SqliteConnection Connection, UnifiedDbContext DbContext)> CreateSqliteDbContextAsync(
        params IInterceptor[] interceptors
    )
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.CreateFunction("now", () => DateTimeOffset.UtcNow.ToString("O"));
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var optionsBuilder = new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite(connection);
        if (interceptors.Length > 0)
        {
            optionsBuilder.AddInterceptors(interceptors);
        }

        var dbContext = new SqliteTestUnifiedDbContext(optionsBuilder.Options);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        return (connection, dbContext);
    }

    private sealed class FakeCurrentActorResolver(Guid actorId, string actorName) : ICurrentActorResolver
    {
        public CurrentActor Resolve() => new(actorId, actorName);
    }
}
