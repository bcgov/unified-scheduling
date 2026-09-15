using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Stats;
using Unified.Db.Models.UserManagement;
using Unified.Stats.Models;
using Unified.Stats.Services;
using Unified.Tests.TestHelpers;

namespace Unified.Tests.Stats.Services;

public sealed class DashboardServiceTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private SqliteTestUnifiedDbContext _db = null!;
    private DashboardService _service = null!;

    private static readonly Guid EmployeeUserId = Guid.NewGuid();
    private static readonly Guid OtherLocationUserId = Guid.NewGuid();
    private static readonly Guid SignOffUserId = Guid.NewGuid();
    private const int HomeLocationId = 1;
    private const int OtherLocationId = 2;
    private const int EmployeeGroupId = 1;
    private const int LocationLevelGroupId = 2;
    private const int MetricRegularId = 1;
    private const int MetricOvertimeId = 2;
    private const int LLMetricId = 10;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _connection.CreateFunction("now", () => DateTimeOffset.UtcNow.ToString("O"));

        var options = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new SqliteTestUnifiedDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        _service = new DashboardService(_db, NullLogger<DashboardService>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private async Task SeedCoreEntitiesAsync()
    {
        _db.Locations.AddRange(
            new Location { Id = HomeLocationId, AgencyId = "LOC-001", Name = "Victoria", Timezone = "America/Vancouver" },
            new Location { Id = OtherLocationId, AgencyId = "LOC-002", Name = "Vancouver", Timezone = "America/Vancouver" }
        );

        _db.Users.AddRange(
            new User
            {
                Id = EmployeeUserId,
                IdirName = "employee",
                IsEnabled = true,
                FirstName = "John",
                LastName = "Doe",
                Email = "john@test.com",
                HomeLocationId = HomeLocationId,
                BadgeNumber = "B001",
            },
            new User
            {
                Id = OtherLocationUserId,
                IdirName = "otheruser",
                IsEnabled = true,
                FirstName = "Jane",
                LastName = "Smith",
                Email = "jane@test.com",
                HomeLocationId = OtherLocationId,
            },
            new User
            {
                Id = SignOffUserId,
                IdirName = "signoff",
                IsEnabled = true,
                FirstName = "Sign",
                LastName = "Off",
                Email = "signoff@test.com",
                HomeLocationId = HomeLocationId,
            }
        );

        // Employee group
        _db.StatGroups.Add(new StatGroup { Id = EmployeeGroupId, Name = "Employee", DisplayOrder = 1, IsLocationLevel = false });
        // Location-level group
        _db.StatGroups.Add(new StatGroup { Id = LocationLevelGroupId, Name = "Location", DisplayOrder = 2, IsLocationLevel = true });

        var empCategory = new StatCategory { Id = 1, GroupId = EmployeeGroupId, Name = "Work Area A", DisplayOrder = 1 };
        var llCategory = new StatCategory { Id = 2, GroupId = LocationLevelGroupId, Name = "Work Area LL", DisplayOrder = 1 };
        _db.StatCategories.AddRange(empCategory, llCategory);

        _db.SubCategories.AddRange(
            new SubCategory { Id = 1, CategoryId = 1, Name = "Sub A", DisplayOrder = 1 },
            new SubCategory { Id = 2, CategoryId = 2, Name = "Sub LL", DisplayOrder = 1 }
        );

        _db.StatMetrics.AddRange(
            new StatMetric { Id = 1, Name = "Regular Hours", UnitOfMeasure = StatMetricUnitOfMeasure.Hours },
            new StatMetric { Id = 2, Name = "Overtime Hours", UnitOfMeasure = StatMetricUnitOfMeasure.Hours, IsOvertime = true }
        );

        _db.SubCategoryMetrics.AddRange(
            new SubCategoryMetric { Id = MetricRegularId, SubCategoryId = 1, MetricId = 1, DisplayOrder = 1 },
            new SubCategoryMetric { Id = MetricOvertimeId, SubCategoryId = 1, MetricId = 2, DisplayOrder = 2 },
            new SubCategoryMetric { Id = LLMetricId, SubCategoryId = 2, MetricId = 1, DisplayOrder = 1 }
        );

        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private StatRecord CreateRecord(
        Guid? userId,
        int locationId,
        int metricId,
        decimal value,
        string status = StatRecordStatus.Draft,
        DateOnly? date = null
    )
    {
        var d = date ?? new DateOnly(2026, 9, 1);
        return new StatRecord
        {
            DateFrom = d,
            DateTo = d,
            PeriodType = "Daily",
            UserId = userId,
            LocationId = locationId,
            SubCategoryMetricId = metricId,
            Value = value,
            Status = status,
        };
    }

    #region GetEntriesAsync

    [Fact]
    public async Task GetEntriesAsync_Should_Return_Records_For_Home_Location_Employees()
    {
        await SeedCoreEntitiesAsync();
        _db.StatRecords.Add(CreateRecord(EmployeeUserId, HomeLocationId, MetricRegularId, 7.0m));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetEntriesAsync(HomeLocationId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal(EmployeeUserId, result.First().UserId);
        Assert.Equal("John Doe", result.First().EmployeeName);
        Assert.Equal("B001", result.First().BadgeNumber);
    }

    [Fact]
    public async Task GetEntriesAsync_Should_Exclude_Records_From_Other_Locations()
    {
        await SeedCoreEntitiesAsync();
        // Record for employee at a different home location
        _db.StatRecords.Add(CreateRecord(OtherLocationUserId, OtherLocationId, MetricRegularId, 7.0m));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetEntriesAsync(HomeLocationId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetEntriesAsync_Should_Include_LocationLevel_Records_For_Same_Location()
    {
        await SeedCoreEntitiesAsync();
        // Location-level record (no userId)
        _db.StatRecords.Add(CreateRecord(null, HomeLocationId, LLMetricId, 3.0m));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetEntriesAsync(HomeLocationId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Null(result.First().UserId);
    }

    [Fact]
    public async Task GetEntriesAsync_Should_Filter_By_Status()
    {
        await SeedCoreEntitiesAsync();
        _db.StatRecords.AddRange(
            CreateRecord(EmployeeUserId, HomeLocationId, MetricRegularId, 7.0m, StatRecordStatus.Draft),
            CreateRecord(EmployeeUserId, HomeLocationId, MetricOvertimeId, 1.0m, StatRecordStatus.Submitted)
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetEntriesAsync(
            HomeLocationId,
            new DashboardEntriesQueryParams { Status = StatRecordStatus.Submitted },
            TestContext.Current.CancellationToken
        );

        Assert.Single(result);
        Assert.Equal(StatRecordStatus.Submitted, result.First().Status);
    }

    [Fact]
    public async Task GetEntriesAsync_Should_Filter_By_DateRange()
    {
        await SeedCoreEntitiesAsync();
        _db.StatRecords.AddRange(
            CreateRecord(EmployeeUserId, HomeLocationId, MetricRegularId, 7.0m, date: new DateOnly(2026, 9, 1)),
            CreateRecord(EmployeeUserId, HomeLocationId, MetricRegularId, 5.0m, date: new DateOnly(2026, 9, 10))
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetEntriesAsync(
            HomeLocationId,
            new DashboardEntriesQueryParams
            {
                FromDate = new DateOnly(2026, 9, 5),
                ToDate = new DateOnly(2026, 9, 15),
            },
            TestContext.Current.CancellationToken
        );

        Assert.Single(result);
        Assert.Equal(5.0m, result.First().Value);
    }

    #endregion

    #region GetSummaryAsync

    [Fact]
    public async Task GetSummaryAsync_Should_Summarize_Hours_And_Counts()
    {
        await SeedCoreEntitiesAsync();
        _db.StatRecords.AddRange(
            CreateRecord(EmployeeUserId, HomeLocationId, MetricRegularId, 7.0m, StatRecordStatus.Submitted),
            CreateRecord(EmployeeUserId, HomeLocationId, MetricRegularId, 3.5m, StatRecordStatus.Draft),
            CreateRecord(EmployeeUserId, HomeLocationId, MetricOvertimeId, 2.0m, StatRecordStatus.Submitted)
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetSummaryAsync(HomeLocationId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(10.5m, result.RegularHours); // 7.0 + 3.5
        Assert.Equal(2.0m, result.OvertimeHours);
        Assert.Equal(2, result.SubmittedCount); // 2 submitted
        Assert.Equal(3, result.TotalEntries);
    }

    #endregion

    #region SignOffAsync

    [Fact]
    public async Task SignOffAsync_Should_Sign_Off_Draft_And_Submitted_Records()
    {
        await SeedCoreEntitiesAsync();
        _db.StatRecords.AddRange(
            CreateRecord(EmployeeUserId, HomeLocationId, MetricRegularId, 7.0m, StatRecordStatus.Draft),
            CreateRecord(EmployeeUserId, HomeLocationId, MetricOvertimeId, 1.0m, StatRecordStatus.Submitted)
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var ids = await _db.StatRecords.Select(r => r.Id).ToListAsync(TestContext.Current.CancellationToken);

        var result = await _service.SignOffAsync(
            HomeLocationId,
            SignOffUserId,
            new DashboardSignOffRequest { EntryIds = ids },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(2, result.SignedOffCount);
        Assert.Equal(ids.OrderBy(x => x), result.SignedOffIds.OrderBy(x => x));

        var records = await _db.StatRecords.ToListAsync(TestContext.Current.CancellationToken);
        Assert.All(records, r =>
        {
            Assert.Equal(StatRecordStatus.SignedOff, r.Status);
            Assert.Equal(SignOffUserId, r.SignedOffByUserId);
            Assert.NotNull(r.SignedOffAt);
        });
    }

    [Fact]
    public async Task SignOffAsync_Should_Not_Sign_Off_Already_SignedOff_Records()
    {
        await SeedCoreEntitiesAsync();
        _db.StatRecords.Add(CreateRecord(EmployeeUserId, HomeLocationId, MetricRegularId, 7.0m, StatRecordStatus.SignedOff));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var ids = await _db.StatRecords.Select(r => r.Id).ToListAsync(TestContext.Current.CancellationToken);

        var result = await _service.SignOffAsync(
            HomeLocationId,
            SignOffUserId,
            new DashboardSignOffRequest { EntryIds = ids },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(0, result.SignedOffCount);
    }

    [Fact]
    public async Task SignOffAsync_Should_Not_Sign_Off_Records_From_Other_Locations()
    {
        await SeedCoreEntitiesAsync();
        _db.StatRecords.Add(CreateRecord(OtherLocationUserId, OtherLocationId, MetricRegularId, 7.0m, StatRecordStatus.Draft));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var ids = await _db.StatRecords.Select(r => r.Id).ToListAsync(TestContext.Current.CancellationToken);

        var result = await _service.SignOffAsync(
            HomeLocationId,
            SignOffUserId,
            new DashboardSignOffRequest { EntryIds = ids },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(0, result.SignedOffCount);
    }

    [Fact]
    public async Task SignOffAsync_Should_Sign_Off_LocationLevel_Records_For_Same_Location()
    {
        await SeedCoreEntitiesAsync();
        _db.StatRecords.Add(CreateRecord(null, HomeLocationId, LLMetricId, 3.0m, StatRecordStatus.Draft));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var ids = await _db.StatRecords.Select(r => r.Id).ToListAsync(TestContext.Current.CancellationToken);

        var result = await _service.SignOffAsync(
            HomeLocationId,
            SignOffUserId,
            new DashboardSignOffRequest { EntryIds = ids },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(1, result.SignedOffCount);
    }

    #endregion
}
