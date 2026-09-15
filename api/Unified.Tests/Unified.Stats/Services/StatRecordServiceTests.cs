using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Stats;
using Unified.Db.Models.UserManagement;
using Unified.Infrastructure.ErrorHandling;
using Unified.Stats.Models;
using Unified.Stats.Services;

namespace Unified.Tests.Stats.Services;

public sealed class StatRecordServiceTests : IAsyncLifetime
{
    private UnifiedDbContext _db = null!;
    private StatRecordService _service = null!;

    private static readonly Guid CallerUserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();
    private const int LocationId = 1;
    private const int EmployeeGroupId = 1;
    private const int LocationLevelGroupId = 2;

    // Metric IDs seeded in the employee group
    private const int MetricId1 = 1;
    private const int MetricId2 = 2;

    // Metric ID seeded in the location-level group
    private const int LLMetricId = 10;

    // A count metric in the employee group
    private const int CountMetricId = 3;

    // An archived metric in the employee group
    private const int ArchivedMetricId = 99;

    public ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new UnifiedDbContext(options);
        _service = new StatRecordService(_db, NullLogger<StatRecordService>.Instance);

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    private async Task SeedCoreEntitiesAsync()
    {
        _db.Locations.Add(new Location
        {
            Id = LocationId,
            AgencyId = "LOC-001",
            Name = "Victoria",
            Timezone = "America/Vancouver",
        });

        _db.Users.AddRange(
            new User
            {
                Id = CallerUserId,
                IdirName = "caller",
                IsEnabled = true,
                FirstName = "Caller",
                LastName = "User",
                Email = "caller@test.com",
                HomeLocationId = LocationId,
            },
            new User
            {
                Id = OtherUserId,
                IdirName = "other",
                IsEnabled = true,
                FirstName = "Other",
                LastName = "User",
                Email = "other@test.com",
                HomeLocationId = LocationId,
            }
        );

        // Employee-level group
        _db.StatGroups.Add(new StatGroup
        {
            Id = EmployeeGroupId,
            Name = "Employee Stats",
            DisplayOrder = 1,
            IsLocationLevel = false,
        });

        // Location-level group
        _db.StatGroups.Add(new StatGroup
        {
            Id = LocationLevelGroupId,
            Name = "Location Stats",
            DisplayOrder = 2,
            IsLocationLevel = true,
        });

        var empCategory = new StatCategory { Id = 1, GroupId = EmployeeGroupId, Name = "Work Area A", DisplayOrder = 1 };
        var llCategory = new StatCategory { Id = 2, GroupId = LocationLevelGroupId, Name = "Work Area LL", DisplayOrder = 1 };
        _db.StatCategories.AddRange(empCategory, llCategory);

        var empSubCategory = new SubCategory { Id = 1, CategoryId = 1, Name = "Sub A", DisplayOrder = 1 };
        var llSubCategory = new SubCategory { Id = 2, CategoryId = 2, Name = "Sub LL", DisplayOrder = 1 };
        _db.SubCategories.AddRange(empSubCategory, llSubCategory);

        var metric = new StatMetric { Id = 1, Name = "Regular Hours", UnitOfMeasure = "hours" };
        var metric2 = new StatMetric { Id = 2, Name = "Overtime Hours", UnitOfMeasure = "hours", IsOvertime = true };
        var metric3 = new StatMetric { Id = 3, Name = "Trip Count", UnitOfMeasure = "count" };
        _db.StatMetrics.AddRange(metric, metric2, metric3);

        _db.SubCategoryMetrics.AddRange(
            new SubCategoryMetric { Id = MetricId1, SubCategoryId = 1, MetricId = 1, DisplayOrder = 1 },
            new SubCategoryMetric { Id = MetricId2, SubCategoryId = 1, MetricId = 2, DisplayOrder = 2 },
            new SubCategoryMetric { Id = CountMetricId, SubCategoryId = 1, MetricId = 3, DisplayOrder = 3 },
            new SubCategoryMetric { Id = LLMetricId, SubCategoryId = 2, MetricId = 1, DisplayOrder = 1 },
            new SubCategoryMetric { Id = ArchivedMetricId, SubCategoryId = 1, MetricId = 1, DisplayOrder = 4, IsArchived = true }
        );

        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    #region SaveDayAsync

    [Fact]
    public async Task SaveDayAsync_Should_Create_New_Records()
    {
        await SeedCoreEntitiesAsync();

        var request = new SaveDayRequest
        {
            Date = new DateOnly(2026, 9, 1),
            LocationId = LocationId,
            UserId = CallerUserId,
            Status = StatRecordStatus.Draft,
            GroupId = EmployeeGroupId,
            Records =
            [
                new SaveDayRecordItem { SubCategoryMetricId = MetricId1, Value = 7.5m },
                new SaveDayRecordItem { SubCategoryMetricId = MetricId2, Value = 1.0m },
            ],
        };

        var result = await _service.SaveDayAsync(request, CallerUserId, false, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.All(result, r =>
        {
            Assert.Equal(new DateOnly(2026, 9, 1), r.DateFrom);
            Assert.Equal(LocationId, r.LocationId);
            Assert.Equal(StatRecordStatus.Draft, r.Status);
        });
    }

    [Fact]
    public async Task SaveDayAsync_Should_Update_Existing_Records()
    {
        await SeedCoreEntitiesAsync();

        // Seed an existing record
        var existing = new StatRecord
        {
            DateFrom = new DateOnly(2026, 9, 1),
            DateTo = new DateOnly(2026, 9, 1),
            PeriodType = "Daily",
            UserId = CallerUserId,
            LocationId = LocationId,
            SubCategoryMetricId = MetricId1,
            Value = 5.0m,
            Status = StatRecordStatus.Draft,
        };
        _db.StatRecords.Add(existing);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new SaveDayRequest
        {
            Date = new DateOnly(2026, 9, 1),
            LocationId = LocationId,
            UserId = CallerUserId,
            Status = StatRecordStatus.Submitted,
            GroupId = EmployeeGroupId,
            Records =
            [
                new SaveDayRecordItem { Id = existing.Id, SubCategoryMetricId = MetricId1, Value = 7.0m },
            ],
        };

        var result = await _service.SaveDayAsync(request, CallerUserId, false, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal(7.0m, result.First().Value);
        Assert.Equal(StatRecordStatus.Submitted, result.First().Status);
    }

    [Fact]
    public async Task SaveDayAsync_Should_Delete_Records_Not_In_Incoming_Set()
    {
        await SeedCoreEntitiesAsync();

        // Seed two existing records
        _db.StatRecords.AddRange(
            new StatRecord
            {
                DateFrom = new DateOnly(2026, 9, 1),
                DateTo = new DateOnly(2026, 9, 1),
                PeriodType = "Daily",
                UserId = CallerUserId,
                LocationId = LocationId,
                SubCategoryMetricId = MetricId1,
                Value = 5.0m,
                Status = StatRecordStatus.Draft,
            },
            new StatRecord
            {
                DateFrom = new DateOnly(2026, 9, 1),
                DateTo = new DateOnly(2026, 9, 1),
                PeriodType = "Daily",
                UserId = CallerUserId,
                LocationId = LocationId,
                SubCategoryMetricId = MetricId2,
                Value = 1.0m,
                Status = StatRecordStatus.Draft,
            }
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Only send one record back (omit MetricId2)
        var request = new SaveDayRequest
        {
            Date = new DateOnly(2026, 9, 1),
            LocationId = LocationId,
            UserId = CallerUserId,
            Status = StatRecordStatus.Draft,
            GroupId = EmployeeGroupId,
            Records =
            [
                new SaveDayRecordItem { SubCategoryMetricId = MetricId1, Value = 5.0m },
            ],
        };

        await _service.SaveDayAsync(request, CallerUserId, false, cancellationToken: TestContext.Current.CancellationToken);

        var remaining = await _db.StatRecords.CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, remaining);
    }

    [Fact]
    public async Task SaveDayAsync_Should_Not_Delete_SignedOff_Records_Without_Override()
    {
        await SeedCoreEntitiesAsync();

        _db.StatRecords.Add(new StatRecord
        {
            DateFrom = new DateOnly(2026, 9, 1),
            DateTo = new DateOnly(2026, 9, 1),
            PeriodType = "Daily",
            UserId = CallerUserId,
            LocationId = LocationId,
            SubCategoryMetricId = MetricId1,
            Value = 5.0m,
            Status = StatRecordStatus.SignedOff,
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Send empty save (new record only) -- should NOT delete the signed-off record
        var request = new SaveDayRequest
        {
            Date = new DateOnly(2026, 9, 1),
            LocationId = LocationId,
            UserId = CallerUserId,
            Status = StatRecordStatus.Draft,
            GroupId = EmployeeGroupId,
            Records =
            [
                new SaveDayRecordItem { SubCategoryMetricId = MetricId2, Value = 1.0m },
            ],
        };

        await _service.SaveDayAsync(request, CallerUserId, false, cancellationToken: TestContext.Current.CancellationToken);

        var remaining = await _db.StatRecords.CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, remaining); // signed-off kept + new one added
    }

    [Fact]
    public async Task SaveDayAsync_Should_Delete_SignedOff_Records_With_Override()
    {
        await SeedCoreEntitiesAsync();

        _db.StatRecords.Add(new StatRecord
        {
            DateFrom = new DateOnly(2026, 9, 1),
            DateTo = new DateOnly(2026, 9, 1),
            PeriodType = "Daily",
            UserId = CallerUserId,
            LocationId = LocationId,
            SubCategoryMetricId = MetricId1,
            Value = 5.0m,
            Status = StatRecordStatus.SignedOff,
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new SaveDayRequest
        {
            Date = new DateOnly(2026, 9, 1),
            LocationId = LocationId,
            UserId = CallerUserId,
            Status = StatRecordStatus.Draft,
            GroupId = EmployeeGroupId,
            Records =
            [
                new SaveDayRecordItem { SubCategoryMetricId = MetricId2, Value = 1.0m },
            ],
        };

        await _service.SaveDayAsync(request, CallerUserId, false, canOverrideSignedOff: true, cancellationToken: TestContext.Current.CancellationToken);

        var remaining = await _db.StatRecords.CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, remaining); // signed-off deleted, new one added
    }

    [Fact]
    public async Task SaveDayAsync_Should_Throw_When_Records_Empty()
    {
        await SeedCoreEntitiesAsync();

        var request = new SaveDayRequest
        {
            Date = new DateOnly(2026, 9, 1),
            LocationId = LocationId,
            UserId = CallerUserId,
            Status = StatRecordStatus.Draft,
            GroupId = EmployeeGroupId,
            Records = [],
        };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SaveDayAsync(request, CallerUserId, false, cancellationToken: TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task SaveDayAsync_Should_Throw_When_Metrics_Do_Not_Belong_To_Group()
    {
        await SeedCoreEntitiesAsync();

        // Try to save a location-level metric via an employee group request
        var request = new SaveDayRequest
        {
            Date = new DateOnly(2026, 9, 1),
            LocationId = LocationId,
            UserId = CallerUserId,
            Status = StatRecordStatus.Draft,
            GroupId = EmployeeGroupId,
            Records =
            [
                new SaveDayRecordItem { SubCategoryMetricId = LLMetricId, Value = 1.0m },
            ],
        };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SaveDayAsync(request, CallerUserId, false, cancellationToken: TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task SaveDayAsync_Should_Throw_When_Archived_Metric_Used()
    {
        await SeedCoreEntitiesAsync();

        var request = new SaveDayRequest
        {
            Date = new DateOnly(2026, 9, 1),
            LocationId = LocationId,
            UserId = CallerUserId,
            Status = StatRecordStatus.Draft,
            GroupId = EmployeeGroupId,
            Records =
            [
                new SaveDayRecordItem { SubCategoryMetricId = ArchivedMetricId, Value = 1.0m },
            ],
        };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SaveDayAsync(request, CallerUserId, false, cancellationToken: TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task SaveDayAsync_Should_Throw_When_Caller_Not_Authorized_For_Other_User()
    {
        await SeedCoreEntitiesAsync();

        var request = new SaveDayRequest
        {
            Date = new DateOnly(2026, 9, 1),
            LocationId = LocationId,
            UserId = OtherUserId,
            Status = StatRecordStatus.Draft,
            GroupId = EmployeeGroupId,
            Records =
            [
                new SaveDayRecordItem { SubCategoryMetricId = MetricId1, Value = 1.0m },
            ],
        };

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.SaveDayAsync(request, CallerUserId, callerCanEnterForOthers: false, cancellationToken: TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task SaveDayAsync_Should_Allow_Caller_To_Enter_For_Others_When_Authorized()
    {
        await SeedCoreEntitiesAsync();

        var request = new SaveDayRequest
        {
            Date = new DateOnly(2026, 9, 1),
            LocationId = LocationId,
            UserId = OtherUserId,
            Status = StatRecordStatus.Draft,
            GroupId = EmployeeGroupId,
            Records =
            [
                new SaveDayRecordItem { SubCategoryMetricId = MetricId1, Value = 1.0m },
            ],
        };

        var result = await _service.SaveDayAsync(request, CallerUserId, callerCanEnterForOthers: true, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal(OtherUserId, result.First().UserId);
    }

    [Fact]
    public async Task SaveDayAsync_Should_Skip_Auth_For_LocationLevel_Group()
    {
        await SeedCoreEntitiesAsync();

        var request = new SaveDayRequest
        {
            Date = new DateOnly(2026, 9, 1),
            LocationId = LocationId,
            UserId = null,
            Status = StatRecordStatus.Draft,
            GroupId = LocationLevelGroupId,
            Records =
            [
                new SaveDayRecordItem { SubCategoryMetricId = LLMetricId, Value = 3.0m },
            ],
        };

        // Should not throw even though callerCanEnterForOthers is false and UserId is null
        var result = await _service.SaveDayAsync(request, CallerUserId, callerCanEnterForOthers: false, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Null(result.First().UserId);
    }

    [Fact]
    public async Task SaveDayAsync_Should_Set_PerformedAtLocationId()
    {
        await SeedCoreEntitiesAsync();

        var request = new SaveDayRequest
        {
            Date = new DateOnly(2026, 9, 1),
            LocationId = LocationId,
            UserId = CallerUserId,
            Status = StatRecordStatus.Draft,
            GroupId = EmployeeGroupId,
            Records =
            [
                new SaveDayRecordItem { SubCategoryMetricId = MetricId1, Value = 7.0m, PerformedAtLocationId = 42 },
            ],
        };

        var result = await _service.SaveDayAsync(request, CallerUserId, false, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(42, result.First().PerformedAtLocationId);
    }

    [Fact]
    public async Task SaveDayAsync_Should_Throw_When_Hours_Not_Quarter_Increment()
    {
        await SeedCoreEntitiesAsync();

        var request = new SaveDayRequest
        {
            Date = new DateOnly(2026, 9, 1),
            LocationId = LocationId,
            UserId = CallerUserId,
            Status = StatRecordStatus.Draft,
            GroupId = EmployeeGroupId,
            Records =
            [
                new SaveDayRecordItem { SubCategoryMetricId = MetricId1, Value = 7.3m },
            ],
        };

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SaveDayAsync(request, CallerUserId, false, cancellationToken: TestContext.Current.CancellationToken)
        );
        Assert.Contains("0.25", ex.Message);
    }

    [Fact]
    public async Task SaveDayAsync_Should_Accept_Valid_Quarter_Hour_Increments()
    {
        await SeedCoreEntitiesAsync();

        var request = new SaveDayRequest
        {
            Date = new DateOnly(2026, 9, 1),
            LocationId = LocationId,
            UserId = CallerUserId,
            Status = StatRecordStatus.Draft,
            GroupId = EmployeeGroupId,
            Records =
            [
                new SaveDayRecordItem { SubCategoryMetricId = MetricId1, Value = 7.75m },
            ],
        };

        var result = await _service.SaveDayAsync(request, CallerUserId, false, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal(7.75m, result.First().Value);
    }

    [Fact]
    public async Task SaveDayAsync_Should_Throw_When_Count_Not_Whole_Number()
    {
        await SeedCoreEntitiesAsync();

        var request = new SaveDayRequest
        {
            Date = new DateOnly(2026, 9, 1),
            LocationId = LocationId,
            UserId = CallerUserId,
            Status = StatRecordStatus.Draft,
            GroupId = EmployeeGroupId,
            Records =
            [
                new SaveDayRecordItem { SubCategoryMetricId = CountMetricId, Value = 2.5m },
            ],
        };

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SaveDayAsync(request, CallerUserId, false, cancellationToken: TestContext.Current.CancellationToken)
        );
        Assert.Contains("whole numbers", ex.Message);
    }

    [Fact]
    public async Task SaveDayAsync_Should_Throw_When_Value_Is_Negative()
    {
        await SeedCoreEntitiesAsync();

        var request = new SaveDayRequest
        {
            Date = new DateOnly(2026, 9, 1),
            LocationId = LocationId,
            UserId = CallerUserId,
            Status = StatRecordStatus.Draft,
            GroupId = EmployeeGroupId,
            Records =
            [
                new SaveDayRecordItem { SubCategoryMetricId = MetricId1, Value = -1.0m },
            ],
        };

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SaveDayAsync(request, CallerUserId, false, cancellationToken: TestContext.Current.CancellationToken)
        );
        Assert.Contains("negative", ex.Message);
    }

    #endregion

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_Should_Create_And_Return_Record()
    {
        await SeedCoreEntitiesAsync();

        var request = new StatRecordRequest
        {
            DateFrom = new DateOnly(2026, 9, 1),
            DateTo = new DateOnly(2026, 9, 1),
            PeriodType = "Daily",
            UserId = CallerUserId,
            LocationId = LocationId,
            SubCategoryMetricId = MetricId1,
            Value = 7.5m,
        };

        var result = await _service.CreateAsync(request, CallerUserId, false, TestContext.Current.CancellationToken);

        Assert.NotEqual(0, result.Id);
        Assert.Equal(7.5m, result.Value);
        Assert.Equal(CallerUserId, result.UserId);
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_Not_Authorized()
    {
        await SeedCoreEntitiesAsync();

        var request = new StatRecordRequest
        {
            DateFrom = new DateOnly(2026, 9, 1),
            DateTo = new DateOnly(2026, 9, 1),
            PeriodType = "Daily",
            UserId = OtherUserId,
            LocationId = LocationId,
            SubCategoryMetricId = MetricId1,
            Value = 7.5m,
        };

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.CreateAsync(request, CallerUserId, false, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task CreateAsync_Should_Trim_Comment()
    {
        await SeedCoreEntitiesAsync();

        var request = new StatRecordRequest
        {
            DateFrom = new DateOnly(2026, 9, 1),
            DateTo = new DateOnly(2026, 9, 1),
            PeriodType = "  Daily  ",
            UserId = CallerUserId,
            LocationId = LocationId,
            SubCategoryMetricId = MetricId1,
            Value = 1.0m,
            Comment = "  some comment  ",
        };

        var result = await _service.CreateAsync(request, CallerUserId, false, TestContext.Current.CancellationToken);

        var entity = await _db.StatRecords.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Daily", entity.PeriodType);
        Assert.Equal("some comment", entity.Comment);
    }

    #endregion

    #region UpdateAsync

    [Fact]
    public async Task UpdateAsync_Should_Update_And_Return_Record()
    {
        await SeedCoreEntitiesAsync();
        var entity = new StatRecord
        {
            DateFrom = new DateOnly(2026, 9, 1),
            DateTo = new DateOnly(2026, 9, 1),
            PeriodType = "Daily",
            UserId = CallerUserId,
            LocationId = LocationId,
            SubCategoryMetricId = MetricId1,
            Value = 5.0m,
            Status = StatRecordStatus.Draft,
        };
        _db.StatRecords.Add(entity);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new StatRecordRequest
        {
            DateFrom = new DateOnly(2026, 9, 2),
            DateTo = new DateOnly(2026, 9, 2),
            PeriodType = "Daily",
            UserId = CallerUserId,
            LocationId = LocationId,
            SubCategoryMetricId = MetricId1,
            Value = 8.0m,
        };

        var result = await _service.UpdateAsync(entity.Id, request, CallerUserId, false, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(8.0m, result.Value);
        Assert.Equal(new DateOnly(2026, 9, 2), result.DateFrom);
    }

    [Fact]
    public async Task UpdateAsync_Should_Return_Null_When_Not_Found()
    {
        await SeedCoreEntitiesAsync();

        var request = new StatRecordRequest
        {
            DateFrom = new DateOnly(2026, 9, 1),
            DateTo = new DateOnly(2026, 9, 1),
            PeriodType = "Daily",
            UserId = CallerUserId,
            LocationId = LocationId,
            SubCategoryMetricId = MetricId1,
            Value = 1.0m,
        };

        var result = await _service.UpdateAsync(999, request, CallerUserId, false, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    #endregion

    #region DeleteAsync

    [Fact]
    public async Task DeleteAsync_Should_Remove_Record()
    {
        await SeedCoreEntitiesAsync();
        var entity = new StatRecord
        {
            DateFrom = new DateOnly(2026, 9, 1),
            DateTo = new DateOnly(2026, 9, 1),
            PeriodType = "Daily",
            UserId = CallerUserId,
            LocationId = LocationId,
            SubCategoryMetricId = MetricId1,
            Value = 5.0m,
            Status = StatRecordStatus.Draft,
        };
        _db.StatRecords.Add(entity);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var deleted = await _service.DeleteAsync(entity.Id, CallerUserId, false, TestContext.Current.CancellationToken);

        Assert.True(deleted);
        Assert.Equal(0, await _db.StatRecords.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteAsync_Should_Return_False_When_Not_Found()
    {
        await SeedCoreEntitiesAsync();

        var deleted = await _service.DeleteAsync(999, CallerUserId, false, TestContext.Current.CancellationToken);

        Assert.False(deleted);
    }

    [Fact]
    public async Task DeleteAsync_Should_Throw_When_Not_Authorized()
    {
        await SeedCoreEntitiesAsync();
        var entity = new StatRecord
        {
            DateFrom = new DateOnly(2026, 9, 1),
            DateTo = new DateOnly(2026, 9, 1),
            PeriodType = "Daily",
            UserId = OtherUserId,
            LocationId = LocationId,
            SubCategoryMetricId = MetricId1,
            Value = 5.0m,
            Status = StatRecordStatus.Draft,
        };
        _db.StatRecords.Add(entity);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.DeleteAsync(entity.Id, CallerUserId, false, TestContext.Current.CancellationToken)
        );
    }

    #endregion

    #region GetAllAsync

    [Fact]
    public async Task GetAllAsync_Should_Filter_By_LocationId()
    {
        await SeedCoreEntitiesAsync();
        _db.StatRecords.AddRange(
            new StatRecord
            {
                DateFrom = new DateOnly(2026, 9, 1),
                DateTo = new DateOnly(2026, 9, 1),
                PeriodType = "Daily",
                UserId = CallerUserId,
                LocationId = LocationId,
                SubCategoryMetricId = MetricId1,
                Value = 5.0m,
                Status = StatRecordStatus.Draft,
            },
            new StatRecord
            {
                DateFrom = new DateOnly(2026, 9, 1),
                DateTo = new DateOnly(2026, 9, 1),
                PeriodType = "Daily",
                UserId = CallerUserId,
                LocationId = 999,
                SubCategoryMetricId = MetricId1,
                Value = 3.0m,
                Status = StatRecordStatus.Draft,
            }
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetAllAsync(
            new StatRecordQueryParams { LocationId = LocationId },
            TestContext.Current.CancellationToken
        );

        Assert.Single(result);
        Assert.Equal(LocationId, result.First().LocationId);
    }

    [Fact]
    public async Task GetAllAsync_Should_Filter_By_Status()
    {
        await SeedCoreEntitiesAsync();
        _db.StatRecords.AddRange(
            new StatRecord
            {
                DateFrom = new DateOnly(2026, 9, 1),
                DateTo = new DateOnly(2026, 9, 1),
                PeriodType = "Daily",
                UserId = CallerUserId,
                LocationId = LocationId,
                SubCategoryMetricId = MetricId1,
                Value = 5.0m,
                Status = StatRecordStatus.Draft,
            },
            new StatRecord
            {
                DateFrom = new DateOnly(2026, 9, 1),
                DateTo = new DateOnly(2026, 9, 1),
                PeriodType = "Daily",
                UserId = CallerUserId,
                LocationId = LocationId,
                SubCategoryMetricId = MetricId2,
                Value = 1.0m,
                Status = StatRecordStatus.Submitted,
            }
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetAllAsync(
            new StatRecordQueryParams { Status = StatRecordStatus.Submitted },
            TestContext.Current.CancellationToken
        );

        Assert.Single(result);
        Assert.Equal(StatRecordStatus.Submitted, result.First().Status);
    }

    #endregion

    #region IsLocationLevelGroupAsync

    [Fact]
    public async Task IsLocationLevelGroupAsync_Should_Return_True_For_LL_Group()
    {
        await SeedCoreEntitiesAsync();

        var result = await _service.IsLocationLevelGroupAsync(LocationLevelGroupId, TestContext.Current.CancellationToken);

        Assert.True(result);
    }

    [Fact]
    public async Task IsLocationLevelGroupAsync_Should_Return_False_For_Employee_Group()
    {
        await SeedCoreEntitiesAsync();

        var result = await _service.IsLocationLevelGroupAsync(EmployeeGroupId, TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    [Fact]
    public async Task IsLocationLevelGroupAsync_Should_Return_False_For_NonExistent_Group()
    {
        await SeedCoreEntitiesAsync();

        var result = await _service.IsLocationLevelGroupAsync(999, TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    #endregion
}
