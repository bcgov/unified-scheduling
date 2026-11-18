using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Unified.DataMigration.Services;
using Unified.DataMigration.Sources;
using Unified.Db;
using Unified.Db.Models.DataMigration;
using Unified.Db.Models.UserManagement;
using Unified.Tests.TestHelpers;

namespace Unified.Tests.DataMigration;

public sealed class SsStaleSsoBindingTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:;Foreign Keys=True");
    private readonly UnifiedDbContext _db;
    private static readonly DateTimeOffset Boundary = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid SharedKeycloak = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid SurvivorIdir = Guid.Parse("20000000-0000-0000-0000-000000000001");

    public SsStaleSsoBindingTests()
    {
        _connection.Open();
        _db = new SqliteTestUnifiedDbContext(
            new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite(_connection).Options
        );
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task DisabledUnboundDuplicate_IsImportedClearedAndChildResolutionRemainsDistinct()
    {
        var users = ValidDuplicate();
        var targetRole = new Role { Name = "TestRole", Description = "" };
        _db.Roles.Add(targetRole);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var rows = Rows(users);
        rows["Role"].Add(RoleRow());
        rows["UserRole"].Add(UserRoleRow(Guid.Parse(users[0].Id)));
        var session = await WriteAsync(rows);

        var imported = await _db.Users.OrderBy(x => x.IdirName).ToListAsync();
        Assert.Equal(2, imported.Count);
        var loser = imported.Single(x => x.IdirName == "loser");
        Assert.False(loser.IsEnabled);
        Assert.Null(loser.IdirId);
        Assert.Null(loser.KeyCloakId);
        var survivor = imported.Single(x => x.IdirName == "survivor");
        Assert.Equal(SurvivorIdir, survivor.IdirId);
        Assert.Equal(SharedKeycloak, survivor.KeyCloakId);
        Assert.Equal(
            "MigratedWithDifference",
            session.Outcomes.Single(x => x.EntityType == "User" && x.LegacyId == users[1].Id).Outcome
        );
        Assert.Equal(
            "StaleSsoBindingCleared",
            session.Outcomes.Single(x => x.EntityType == "User" && x.LegacyId == users[1].Id).Reason
        );

        var firstTarget = await session.ResolveAsync("User", users[0].Id, TestContext.Current.CancellationToken);
        var secondTarget = await session.ResolveAsync("User", users[1].Id, TestContext.Current.CancellationToken);
        Assert.NotEqual(firstTarget, secondTarget);
        Assert.Equal(
            Guid.Parse(firstTarget),
            (await _db.UserRoles.SingleAsync(TestContext.Current.CancellationToken)).UserId
        );
    }

    [Theory]
    [InlineData("multiple-enabled")]
    [InlineData("bound-loser")]
    [InlineData("non-unique-survivor-idir")]
    [InlineData("normalized-name-duplicate")]
    [InlineData("live-history")]
    [InlineData("history-unavailable")]
    public async Task UnsafeDuplicateGroup_IsRejectedAsAWhole(string scenario)
    {
        var users = ValidDuplicate();
        switch (scenario)
        {
            case "multiple-enabled":
                users[1] = UserRow(2, "loser", enabled: true, idir: null, keycloak: SharedKeycloak);
                break;
            case "bound-loser":
                users[1] = UserRow(
                    2,
                    "loser",
                    enabled: false,
                    idir: Guid.Parse("20000000-0000-0000-0000-000000000002"),
                    keycloak: SharedKeycloak
                );
                break;
            case "non-unique-survivor-idir":
                users.Add(UserRow(3, "unrelated", false, SurvivorIdir, null));
                break;
            case "normalized-name-duplicate":
                users[1] = UserRow(2, " SURVIVOR ", enabled: false, idir: null, keycloak: SharedKeycloak);
                break;
            case "live-history":
                break;
        }

        var rows = Rows(users);
        if (scenario == "history-unavailable")
            rows.Remove("__UsersWithHistory");
        if (scenario == "live-history")
        {
            rows["SheriffTraining"]
                .Add(
                    new SsRow(
                        new Dictionary<string, object?>
                        {
                            ["Id"] = 1,
                            ["SheriffId"] = Guid.Parse(users[1].Id),
                            ["EndDate"] = new DateTime(2026, 1, 1),
                            ["ExpiryDate"] = null,
                        }
                    )
                );
            rows["__UsersWithHistory"]
                .Add(new SsRow(new Dictionary<string, object?> { ["Id"] = Guid.Parse(users[1].Id) }));
        }

        var session = await WriteAsync(rows);
        var userOutcomes = session.Outcomes.Where(x => x.EntityType == "User").ToList();
        Assert.Equal(users.Count, userOutcomes.Count);
        Assert.All(
            userOutcomes.Where(x => users.Take(2).Any(user => user.Id == x.LegacyId)),
            outcome =>
            {
                Assert.Equal("Rejected", outcome.Outcome);
                Assert.Equal(
                    scenario == "normalized-name-duplicate" ? "DuplicateNormalizedIdirName"
                        : scenario == "live-history" ? "StaleSsoBindingHasLiveHistory"
                        : scenario == "history-unavailable" ? "StaleSsoBindingHistoryUnavailable"
                        : "DuplicateSsoIdentity",
                    outcome.Reason
                );
            }
        );
        Assert.Empty(await _db.Users.ToListAsync());
    }

    [Fact]
    public async Task StaleBindingIdentityReplay_IsIdempotent()
    {
        var users = ValidDuplicate();
        var first = await WriteAsync(users);
        var initialTargetIds = await _db.Users.OrderBy(x => x.IdirName).Select(x => x.Id).ToListAsync();
        var initialMutationCount = await _db.DataMigrationMutations.CountAsync();

        var rerun = await NewSessionAsync();
        await new SsMyTeamWriter(_db, new SsLeaveWriter(_db)).WriteAsync(
            Rows(users),
            rerun,
            Boundary,
            TestContext.Current.CancellationToken
        );
        await CommitCurrentAsync();

        Assert.Equal(initialTargetIds, await _db.Users.OrderBy(x => x.IdirName).Select(x => x.Id).ToListAsync());
        Assert.Equal(initialMutationCount, await _db.DataMigrationMutations.CountAsync());
        Assert.Equal(1, rerun.Outcomes.Count(x => x.EntityType == "User" && x.Outcome == "Migrated"));
        Assert.Equal(
            "StaleSsoBindingCleared",
            rerun.Outcomes.Single(x => x.EntityType == "User" && x.LegacyId == users[1].Id).Reason
        );
        Assert.Equal(2, first.Outcomes.Count(x => x.EntityType == "User"));
    }

    private async Task<SsWriteSession> WriteAsync(List<SsRow> users) => await WriteAsync(Rows(users));

    private async Task<SsWriteSession> WriteAsync(Dictionary<string, List<SsRow>> rows)
    {
        var session = await NewSessionAsync();
        await new SsMyTeamWriter(_db, new SsLeaveWriter(_db)).WriteAsync(
            rows,
            session,
            Boundary,
            TestContext.Current.CancellationToken
        );
        await CommitCurrentAsync();
        return session;
    }

    private async Task<SsWriteSession> NewSessionAsync()
    {
        var run = new DataMigrationRun
        {
            Source = "SS",
            Job = "MyTeam",
            Status = "Running",
            StartedOn = DateTimeOffset.UtcNow,
        };
        _db.DataMigrationRuns.Add(run);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var tx = await _db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        // Keep a per-session transaction alive through the writer's savepoints and writes.
        _transactions.Add(tx);
        return new SsWriteSession(_db, run);
    }

    private readonly List<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> _transactions = [];

    private async Task CommitCurrentAsync() =>
        await _transactions[^1].CommitAsync(TestContext.Current.CancellationToken);

    private static List<SsRow> ValidDuplicate() =>
        [UserRow(1, "survivor", true, SurvivorIdir, SharedKeycloak), UserRow(2, "loser", false, null, SharedKeycloak)];

    private static SsRow UserRow(int id, string name, bool enabled, Guid? idir, Guid? keycloak) =>
        new(
            new Dictionary<string, object?>
            {
                ["Id"] = Guid.Parse($"30000000-0000-0000-0000-{id.ToString("D12")}"),
                ["IdirName"] = name,
                ["IdirId"] = idir,
                ["KeyCloakId"] = keycloak,
                ["HomeLocationId"] = null,
                ["IsEnabled"] = enabled,
                ["Gender"] = 0,
                ["LastLogin"] = null,
                ["FirstName"] = name,
                ["LastName"] = "Example",
                ["Email"] = $"{id}@example.invalid",
                ["BadgeNumber"] = null,
                ["Rank"] = null,
                ["Photo"] = null,
                ["LastPhotoUpdate"] = null,
                ["UpdatedOn"] = null,
                ["CreatedOn"] = null,
                ["Excused"] = false,
            }
        );

    private static SsRow RoleRow() => new(new Dictionary<string, object?> { ["Id"] = 99, ["Name"] = "TestRole" });

    private static SsRow UserRoleRow(Guid userId) =>
        new(
            new Dictionary<string, object?>
            {
                ["Id"] = 1,
                ["UserId"] = userId,
                ["RoleId"] = 99,
                ["EffectiveDate"] = new DateTime(2026, 1, 1),
                ["ExpiryDate"] = null,
                ["ExpiryReason"] = null,
                ["UpdatedOn"] = null,
                ["CreatedOn"] = null,
            }
        );

    private static Dictionary<string, List<SsRow>> Rows(List<SsRow> users) =>
        new(StringComparer.Ordinal)
        {
            ["Region"] = [],
            ["Location"] = [],
            ["LookupCode"] = [],
            ["LookupSortOrder"] = [],
            ["Role"] = [],
            ["User"] = users,
            ["UserRole"] = [],
            ["SheriffActingRank"] = [],
            ["SheriffAwayLocation"] = [],
            ["SheriffLeave"] = [],
            ["SheriffTraining"] = [],
            ["DutySlot"] = [],
            ["Shift"] = [],
            ["__UsersWithHistory"] = [],
        };

    public async ValueTask DisposeAsync()
    {
        foreach (var transaction in _transactions)
            await transaction.DisposeAsync();
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
