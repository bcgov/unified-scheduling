using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Unified.Calendar.Services;
using Unified.Common.Time;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Lookup;
using Unified.Db.Models.Scheduling;
using Unified.Db.Models.Stats;
using Unified.Db.Models.UserManagement;
using Unified.Scheduling;
using Unified.Scheduling.Models;
using Unified.Scheduling.Options;
using Unified.Scheduling.Services;
using Unified.Tests.TestHelpers;

namespace Unified.Tests.Scheduling.Services;

public sealed class WorkingHoursServiceTests : IAsyncLifetime
{
    private static readonly Guid UserA = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserB = new("22222222-2222-2222-2222-222222222222");
    private static readonly DateOnly BusinessDate = new(2026, 6, 1);

    private SqliteConnection connection = null!;
    private UnifiedDbContext db = null!;
    private WorkingHoursService service = null!;

    public async ValueTask InitializeAsync()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var dbOptions = new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite(connection).Options;
        db = new SqliteTestUnifiedDbContext(dbOptions);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await SeedBaseDataAsync();

        service = new WorkingHoursService(
            NullLogger<WorkingHoursService>.Instance,
            db,
            Options.Create(
                new WorkingHoursOptions
                {
                    FullWorkingDayMinutes = 420,
                    DefaultLunchMinutes = 60,
                    MaxQueryRangeDays = 31,
                }
            ),
            new TimeZoneService()
        );
    }

    public async ValueTask DisposeAsync()
    {
        await db.DisposeAsync();
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task QueryAsync_NormalShiftWithoutOvertime_ReturnsPaidShiftMinutesOnly()
    {
        // Arrange
        await AddShiftAsync(At(9), At(17));

        // Act
        var result = Assert.Single(await QueryAsync());

        // Assert
        Assert.Equal(420, result.PaidShiftMinutes);
        Assert.Equal(0, result.PaidOutsideShiftMinutes);
        Assert.Equal(420, result.CreditedMinutes);
        Assert.Equal(0, result.OvertimeMinutes);
    }

    [Fact]
    public async Task QueryAsync_PreShiftAssignment_CountsOnlyPreShiftMinutes()
    {
        // Arrange
        var shift = await AddShiftAsync(At(9), At(17));
        await AddAssignmentAsync(shift, At(8), At(10));

        // Act
        var result = Assert.Single(await QueryAsync());

        // Assert
        Assert.Equal(60, result.PaidOutsideShiftMinutes);
        Assert.Equal(480, result.CreditedMinutes);
        Assert.Equal(60, result.OvertimeMinutes);
    }

    [Fact]
    public async Task QueryAsync_PostShiftAssignment_CountsOnlyPostShiftMinutes()
    {
        // Arrange
        var shift = await AddShiftAsync(At(9), At(17));
        await AddAssignmentAsync(shift, At(16), At(18));

        // Act
        var result = Assert.Single(await QueryAsync());

        // Assert
        Assert.Equal(60, result.PaidOutsideShiftMinutes);
        Assert.Equal(480, result.CreditedMinutes);
        Assert.Equal(60, result.OvertimeMinutes);
    }

    [Fact]
    public async Task QueryAsync_AssignmentSpansBothSidesOfShift_CountsBothOutsidePortions()
    {
        // Arrange
        var shift = await AddShiftAsync(At(9), At(17));
        await AddAssignmentAsync(shift, At(8), At(18));

        // Act
        var result = Assert.Single(await QueryAsync());

        // Assert
        Assert.Equal(120, result.PaidOutsideShiftMinutes);
        Assert.Equal(540, result.CreditedMinutes);
        Assert.Equal(120, result.OvertimeMinutes);
    }

    [Fact]
    public async Task QueryAsync_OverlappingOutsideShiftAssignments_DoesNotDoubleCountOverlap()
    {
        // Arrange
        var shift = await AddShiftAsync(At(9), At(17));
        await AddAssignmentAsync(shift, At(7), At(8, 30));
        await AddAssignmentAsync(shift, At(8), At(10));

        // Act
        var result = Assert.Single(await QueryAsync());

        // Assert
        Assert.Equal(120, result.PaidOutsideShiftMinutes);
        Assert.Equal(540, result.CreditedMinutes);
    }

    [Fact]
    public async Task QueryAsync_NonOverlappingOutsideShiftAssignments_CountsBothAssignments()
    {
        // Arrange
        var shift = await AddShiftAsync(At(9), At(17));
        await AddAssignmentAsync(shift, At(8), At(9));
        await AddAssignmentAsync(shift, At(17), At(18));

        // Act
        var result = Assert.Single(await QueryAsync());

        // Assert
        Assert.Equal(120, result.PaidOutsideShiftMinutes);
        Assert.Equal(540, result.CreditedMinutes);
    }

    [Fact]
    public async Task QueryAsync_AssignmentForOneUserOnSharedShift_DoesNotAffectOtherUser()
    {
        // Arrange
        var shift = await AddShiftAsync(At(9), At(17), userIds: [UserA, UserB]);
        await AddAssignmentAsync(shift, At(8), At(9), [UserA]);

        // Act
        var results = await QueryAsync();

        // Assert
        Assert.Equal(60, Assert.Single(results, result => result.UserId == UserA).PaidOutsideShiftMinutes);
        Assert.Equal(0, Assert.Single(results, result => result.UserId == UserB).PaidOutsideShiftMinutes);
    }

    [Fact]
    public async Task QueryAsync_MultiUserShift_ReturnsOneResultPerUser()
    {
        // Arrange
        await AddShiftAsync(At(9), At(17), userIds: [UserA, UserB]);

        // Act
        var results = await QueryAsync();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal([UserA, UserB], results.Select(result => result.UserId).Order().ToArray());
    }

    [Fact]
    public async Task QueryAsync_FullyWorkedLunch_ContributesTowardOvertimeThreshold()
    {
        // Arrange
        await AddShiftAsync(At(9), At(17), lunchAvailableMinutes: 60, workedLunchMinutes: 60);

        // Act
        var result = Assert.Single(await QueryAsync());

        // Assert
        Assert.Equal(480, result.PaidShiftMinutes);
        Assert.Equal(60, result.WorkedLunchMinutes);
        Assert.Equal(60, result.OvertimeMinutes);
    }

    [Fact]
    public async Task QueryAsync_PartiallyWorkedLunch_CountsOnlyWorkedLunchMinutes()
    {
        // Arrange
        await AddShiftAsync(At(9), At(17), lunchAvailableMinutes: 60, workedLunchMinutes: 30);

        // Act
        var result = Assert.Single(await QueryAsync());

        // Assert
        Assert.Equal(450, result.PaidShiftMinutes);
        Assert.Equal(30, result.WorkedLunchMinutes);
        Assert.Equal(30, result.OvertimeMinutes);
    }

    [Fact]
    public async Task QueryAsync_OvernightShift_UsesShiftStartBusinessDate()
    {
        // Arrange
        await AddShiftAsync(At(23), AtDay(2, 7));

        // Act
        var result = Assert.Single(await QueryAsync());

        // Assert
        Assert.Equal(BusinessDate, result.Date);
        Assert.Equal(420, result.PaidShiftMinutes);
    }

    [Fact]
    public async Task QueryAsync_UtcStartAfterMidnightButLocalStartBeforeMidnight_UsesLocalBusinessDate()
    {
        // Arrange
        await AddShiftAsync(
            AtDay(2, 6, 30),
            AtDay(2, 14, 30),
            timeZoneId: "America/Vancouver"
        );

        // Act
        var result = Assert.Single(await QueryAsync());

        // Assert
        Assert.Equal(BusinessDate, result.Date);
    }

    [Fact]
    public async Task QueryAsync_LocationAndUserFilters_ReturnsTheirIntersection()
    {
        // Arrange
        await AddShiftAsync(At(9), At(17), locationId: 5, userIds: [UserA]);
        await AddShiftAsync(At(10), At(18), locationId: 5, userIds: [UserB]);
        await AddShiftAsync(At(11), At(19), locationId: 9, userIds: [UserB]);

        // Act
        var result = Assert.Single(
            await QueryAsync(
                new WorkingHoursQuery
                {
                    StartDate = BusinessDate,
                    EndDate = BusinessDate,
                    ShiftLocationIds = [5],
                    UserIds = [UserB],
                }
            )
        );

        // Assert
        Assert.Equal(UserB, result.UserId);
        Assert.Equal(420, result.PaidShiftMinutes);
    }

    [Fact]
    public async Task QueryAsync_CancelledShift_ExcludesShift()
    {
        // Arrange
        await AddShiftAsync(At(9), At(17), statusTypeCode: CalendarEventStatusTypeCodes.Cancelled);

        // Act
        var results = await QueryAsync();

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task QueryAsync_CancelledAssignment_ExcludesAssignmentMinutes()
    {
        // Arrange
        var shift = await AddShiftAsync(At(9), At(17));
        await AddAssignmentAsync(
            shift,
            At(8),
            At(9),
            statusTypeCode: CalendarEventStatusTypeCodes.Cancelled
        );

        // Act
        var result = Assert.Single(await QueryAsync());

        // Assert
        Assert.Equal(0, result.PaidOutsideShiftMinutes);
        Assert.Equal(420, result.CreditedMinutes);
    }

    [Fact]
    public async Task QueryAsync_DateRangeExceedsConfiguredMaximum_ThrowsArgumentException()
    {
        // Arrange
        var query = new WorkingHoursQuery
        {
            StartDate = BusinessDate,
            EndDate = BusinessDate.AddDays(31),
        };

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => QueryAsync(query));

        // Assert
        Assert.Contains("cannot exceed 31 days", exception.Message);
    }

    [Fact]
    public async Task OnMaterializedEventCreatedAsync_ShiftSeriesLunchValuesDiffer_PreservesBothValuesIndependently()
    {
        // Arrange
        var shiftSeries = new ShiftSeries
        {
            LunchAvailableMinutes = 45,
            WorkedLunchMinutes = 15,
        };
        var eventSeries = new EventSeries
        {
            Title = "Series",
            StartAtUtc = At(9),
            EndAtUtc = At(17),
        };
        var eventEntity = new Event
        {
            Title = "Occurrence",
            StartAtUtc = At(9),
            EndAtUtc = At(17),
        };
        var handler = new ShiftSeriesMaterializationHandler(db);

        // Act
        await handler.OnMaterializedEventCreatedAsync(
            eventSeries,
            eventEntity,
            new SeriesEntry { StartAtUtc = At(9), EndAtUtc = At(17) },
            new ShiftSeriesMaterializationContext { ShiftSeries = shiftSeries, UserIds = [UserA] },
            TestContext.Current.CancellationToken
        );

        // Assert
        var entry = Assert.Single(db.ShiftEntries.Local);
        Assert.Equal(45, entry.LunchAvailableMinutes);
        Assert.Equal(15, entry.WorkedLunchMinutes);
    }

    private async Task<ShiftEntry> AddShiftAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        int locationId = 5,
        IReadOnlyCollection<Guid>? userIds = null,
        int lunchAvailableMinutes = 60,
        int workedLunchMinutes = 0,
        string timeZoneId = "UTC",
        string statusTypeCode = CalendarEventStatusTypeCodes.Active
    )
    {
        var shift = new ShiftEntry
        {
            Event = new Event
            {
                Title = "Shift",
                StartAtUtc = start,
                EndAtUtc = end,
                TimeZoneId = timeZoneId,
                EventTypeCode = SchedulingConstants.ShiftEventTypeCode,
                StatusTypeCode = statusTypeCode,
                SourceModule = SchedulingConstants.SourceModule,
                LocationId = locationId,
            },
            LunchAvailableMinutes = lunchAvailableMinutes,
            WorkedLunchMinutes = workedLunchMinutes,
            Users = (userIds ?? [UserA]).Select(userId => new ShiftEntryUser { UserId = userId }).ToList(),
        };

        db.ShiftEntries.Add(shift);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return shift;
    }

    private async Task AddAssignmentAsync(
        ShiftEntry shift,
        DateTimeOffset start,
        DateTimeOffset end,
        IReadOnlyCollection<Guid>? userIds = null,
        string statusTypeCode = CalendarEventStatusTypeCodes.Active
    )
    {
        var assignment = new AssignmentEntry
        {
            AssignmentDefinitionId = 100,
            Event = new Event
            {
                Title = "Assignment",
                StartAtUtc = start,
                EndAtUtc = end,
                TimeZoneId = "UTC",
                EventTypeCode = SchedulingConstants.AssignmentEventTypeCode,
                StatusTypeCode = statusTypeCode,
                SourceModule = SchedulingConstants.SourceModule,
                LocationId = 5,
            },
            Capacity = 10,
            CategoryId = 10,
            SubCategoryId = 11,
        };
        var link = new ShiftAssignmentEntry
        {
            ShiftEntry = shift,
            AssignmentEntry = assignment,
            Users = (userIds ?? [UserA])
                .Select(userId => new ShiftAssignmentEntryUser { UserId = userId })
                .ToList(),
        };

        db.ShiftAssignmentEntries.Add(link);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private Task<IReadOnlyCollection<WorkingHoursResult>> QueryAsync(WorkingHoursQuery? query = null) =>
        service.QueryAsync(
            query
                ?? new WorkingHoursQuery
                {
                    StartDate = BusinessDate,
                    EndDate = BusinessDate,
                },
            TestContext.Current.CancellationToken
        );

    private async Task SeedBaseDataAsync()
    {
        db.EventTypes.AddRange(
            CreateEventType(SchedulingConstants.ShiftEventTypeCode),
            CreateEventType(SchedulingConstants.AssignmentEventTypeCode)
        );
        db.EventStatusTypes.AddRange(
            CreateStatusType(CalendarEventStatusTypeCodes.Active),
            CreateStatusType(CalendarEventStatusTypeCodes.Cancelled)
        );
        db.Locations.AddRange(
            new Location
            {
                Id = 5,
                AgencyId = "A5",
                Name = "Location 5",
                Timezone = "America/Vancouver",
            },
            new Location
            {
                Id = 9,
                AgencyId = "A9",
                Name = "Location 9",
                Timezone = "America/Toronto",
            }
        );
        db.Users.AddRange(CreateUser(UserA, "A"), CreateUser(UserB, "B"));
        db.StatGroups.Add(new StatGroup { Id = 1, Name = "Group" });
        db.StatCategories.Add(
            new StatCategory
            {
                Id = 10,
                GroupId = 1,
                Name = "Category",
            }
        );
        db.SubCategories.Add(
            new SubCategory
            {
                Id = 11,
                CategoryId = 10,
                Name = "Subcategory",
            }
        );
        db.AssignmentDefinitions.Add(
            new AssignmentDefinition
            {
                Id = 100,
                LocationId = 5,
                Name = "Definition",
                NormalizedName = "DEFINITION",
                CategoryId = 10,
                SubCategoryId = 11,
                DefaultCapacity = 10,
                EffectiveDateUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            }
        );

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static DateTimeOffset At(int hour, int minute = 0) =>
        new(2026, 6, 1, hour, minute, 0, TimeSpan.Zero);

    private static DateTimeOffset AtDay(int day, int hour, int minute = 0) =>
        new(2026, 6, day, hour, minute, 0, TimeSpan.Zero);

    private static EventType CreateEventType(string code) =>
        new()
        {
            Code = code,
            Description = code,
            EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };

    private static EventStatusType CreateStatusType(string code) =>
        new()
        {
            Code = code,
            Description = code,
            EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };

    private static User CreateUser(Guid id, string name) =>
        new()
        {
            Id = id,
            IdirName = name,
            IsEnabled = true,
            FirstName = name,
            LastName = "Test",
            Email = $"{name}@example.com",
            Gender = Gender.Other,
        };
}
