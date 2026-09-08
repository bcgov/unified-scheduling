using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Unified.Calendar.Options;
using Unified.Calendar.Services;
using Unified.Common.Time;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Lookup;
using Unified.Db.Models.UserManagement;
using Unified.UserManagement;
using Unified.UserManagement.Models;
using Unified.UserManagement.Services;

namespace Unified.Tests.UserManagement.Services;

public class LeaveCalendarServiceTests : IAsyncLifetime
{
    private static readonly Guid UserA = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserB = new("22222222-2222-2222-2222-222222222222");

    private UnifiedDbContext _dbContext = null!;
    private LeaveCalendarService _service = null!;

    public ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new UnifiedDbContext(options);

        var timeZoneService = new TimeZoneService();
        var timeZoneResolver = new CalendarTimeZoneResolver(
            Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "America/Vancouver" }),
            timeZoneService
        );

        _service = new LeaveCalendarService(
            NullLogger<LeaveCalendarService>.Instance,
            _dbContext,
            timeZoneResolver,
            timeZoneService
        );

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    private async Task<UserLeave> AddLeaveAsync(
        Guid userId,
        DateTimeOffset startAtUtc,
        DateTimeOffset? endAtUtc = null,
        string statusTypeCode = CalendarEventStatusTypeCodes.Active,
        string eventTypeCode = UserManagementConstants.LeaveEventTypeCode,
        string sourceModule = UserManagementConstants.SourceModule,
        string leaveTypeCode = "VAC"
    )
    {
        var leaveType = await _dbContext.LeaveTypes.SingleOrDefaultAsync(
            x => x.Code == leaveTypeCode,
            TestContext.Current.CancellationToken
        );
        if (leaveType is null)
        {
            leaveType = new LeaveType
            {
                Code = leaveTypeCode,
                Description = leaveTypeCode,
                IsPaid = true,
                EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            };
            _dbContext.LeaveTypes.Add(leaveType);
            await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var calendarEvent = new Event
        {
            Title = "Leave",
            StartAtUtc = startAtUtc,
            EndAtUtc = endAtUtc,
            TimeZoneId = "America/Vancouver",
            EventTypeCode = eventTypeCode,
            StatusTypeCode = statusTypeCode,
            SourceModule = sourceModule,
        };
        _dbContext.Events.Add(calendarEvent);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var userLeave = new UserLeave
        {
            UserId = userId,
            EventId = calendarEvent.Id,
            LeaveTypeId = leaveType.Id,
        };
        _dbContext.UserLeaves.Add(userLeave);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return userLeave;
    }

    [Fact]
    public async Task GetLeaveCalendarDataAsync_WhenFiltersProvided_ReturnsOnlyMatchingLeaveEventsForRequestedUsers()
    {
        // Arrange
        var rangeStart = new DateTimeOffset(2026, 6, 1, 7, 0, 0, TimeSpan.Zero);

        var matching = await AddLeaveAsync(UserA, rangeStart.AddHours(2), rangeStart.AddHours(10));
        await AddLeaveAsync(UserB, rangeStart.AddHours(3), rangeStart.AddHours(11));
        await AddLeaveAsync(UserA, rangeStart.AddHours(4), rangeStart.AddHours(12), sourceModule: "calendar");
        await AddLeaveAsync(
            UserA,
            rangeStart.AddHours(5),
            rangeStart.AddHours(13),
            eventTypeCode: CalendarEventTypeCodes.General
        );
        await AddLeaveAsync(
            UserA,
            rangeStart.AddHours(6),
            rangeStart.AddHours(14),
            statusTypeCode: CalendarEventStatusTypeCodes.Cancelled
        );

        var request = new LeaveCalendarRequest
        {
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 6, 3),
            TimeZoneId = "America/Vancouver",
            UserIds = [UserA],
        };

        // Act
        var result = await _service.GetLeaveCalendarDataAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var item = Assert.Single(result.Events);
        Assert.Equal(matching.Id, item.LeaveId);
        Assert.Equal(matching.EventId, item.EventId);
        Assert.Equal(UserA, item.UserId);
        Assert.Equal(UserManagementConstants.SourceModule, item.SourceModule);
        Assert.Equal("user-management.leave", item.Type);
        Assert.Equal(UserManagementConstants.LeaveEventTypeCode, item.EventTypeCode);
    }

    [Fact]
    public async Task GetLeaveCalendarDataAsync_WhenUserIdsNotProvided_ReturnsLeavesForAllUsers()
    {
        // Arrange
        var rangeStart = new DateTimeOffset(2026, 6, 1, 7, 0, 0, TimeSpan.Zero);
        await AddLeaveAsync(UserA, rangeStart.AddHours(1), rangeStart.AddHours(5));
        await AddLeaveAsync(UserB, rangeStart.AddHours(2), rangeStart.AddHours(6));

        var request = new LeaveCalendarRequest
        {
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 6, 2),
            TimeZoneId = "America/Vancouver",
        };

        // Act
        var result = await _service.GetLeaveCalendarDataAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Events.Count);
    }

    [Fact]
    public async Task GetLeaveCalendarDataAsync_WhenLeaveIsOpenEnded_IsIncludedWhenStartWithinRange()
    {
        // Arrange — inclusive range 6/1–6/2 => utc [6/1 07:00, 6/3 07:00)
        var seeded = await AddLeaveAsync(
            UserA,
            startAtUtc: new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero),
            endAtUtc: null
        );

        var request = new LeaveCalendarRequest
        {
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 6, 2),
            TimeZoneId = "America/Vancouver",
        };

        // Act
        var result = await _service.GetLeaveCalendarDataAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var item = Assert.Single(result.Events);
        Assert.Equal(seeded.Id, item.LeaveId);
        Assert.Null(item.End);
    }

    [Fact]
    public async Task GetLeaveCalendarDataAsync_WhenOpenEndedLeaveStartsAtExclusiveEndBoundary_IsExcluded()
    {
        // Arrange — inclusive range 6/1–6/2 => utc end boundary is exclusive at 6/3 07:00
        await AddLeaveAsync(UserA, startAtUtc: new DateTimeOffset(2026, 6, 3, 7, 0, 0, TimeSpan.Zero), endAtUtc: null);

        var request = new LeaveCalendarRequest
        {
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 6, 2),
            TimeZoneId = "America/Vancouver",
        };

        // Act
        var result = await _service.GetLeaveCalendarDataAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result.Events);
    }

    [Fact]
    public async Task GetLeaveCalendarDataAsync_WhenTimeZoneMissing_UsesDefaultTimeZone()
    {
        // Arrange
        var startAtUtc = new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);
        var seeded = await AddLeaveAsync(UserA, startAtUtc, startAtUtc.AddHours(1));
        var request = new LeaveCalendarRequest
        {
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 6, 2),
        };

        // Act
        var result = await _service.GetLeaveCalendarDataAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var item = Assert.Single(result.Events);
        Assert.Equal(seeded.Id, item.LeaveId);
    }

    [Fact]
    public async Task GetLeaveCalendarDataAsync_WhenNoLeavesInRange_ReturnsEmpty()
    {
        // Arrange
        var request = new LeaveCalendarRequest
        {
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 6, 2),
        };

        // Act
        var result = await _service.GetLeaveCalendarDataAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result.Events);
    }

    [Fact]
    public async Task GetLeaveCalendarDataAsync_MapsLeaveTypeCodeAndIsPaidFromLeaveType()
    {
        // Arrange
        var startAtUtc = new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);
        _dbContext.LeaveTypes.Add(
            new LeaveType
            {
                Code = "SICK",
                Description = "Sick",
                IsPaid = false,
                EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            }
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var seeded = await AddLeaveAsync(UserA, startAtUtc, startAtUtc.AddHours(8), leaveTypeCode: "SICK");
        var request = new LeaveCalendarRequest
        {
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 6, 2),
        };

        // Act
        var result = await _service.GetLeaveCalendarDataAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var item = Assert.Single(result.Events);
        Assert.Equal(seeded.LeaveTypeId, item.LeaveTypeId);
        Assert.Equal("SICK", item.LeaveTypeCode);
        Assert.False(item.IsPaid);
    }
}
