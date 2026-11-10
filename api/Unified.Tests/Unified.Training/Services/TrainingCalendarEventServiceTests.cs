using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Training;
using Unified.Db.Models.UserManagement;
using Unified.Tests.TestHelpers;
using Unified.Training.Models.Calendar;
using Unified.Training.Services;

namespace Unified.Tests.Training.Services;

public sealed class TrainingCalendarEventServiceTests : IAsyncLifetime
{
    private readonly Guid _userA = Guid.NewGuid();
    private readonly Guid _userB = Guid.NewGuid();
    private readonly Guid _userC = Guid.NewGuid();
    private UnifiedDbContext _db = null!;
    private TrainingCalendarEventService _service = null!;

    public async ValueTask InitializeAsync()
    {
        _db = new SqliteTestUnifiedDbContext(
            new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite("Data Source=:memory:").Options
        );
        await _db.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await _db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        _db.Locations.AddRange(
            new Location
            {
                Id = 1,
                AgencyId = "A1",
                Name = "One",
                Timezone = "America/Vancouver",
            },
            new Location
            {
                Id = 2,
                AgencyId = "A2",
                Name = "Two",
                Timezone = "America/Vancouver",
            }
        );
        _db.Users.AddRange(
            new User
            {
                Id = _userA,
                IdirName = "training-a",
                HomeLocationId = 1,
            },
            new User
            {
                Id = _userB,
                IdirName = "training-b",
                HomeLocationId = 2,
            },
            new User
            {
                Id = _userC,
                IdirName = "training-c",
                HomeLocationId = 2,
            }
        );
        _db.Trainings.Add(
            new global::Unified.Db.Models.Training.Training
            {
                Id = 1,
                Code = "FIRST-AID",
                Description = string.Empty,
            }
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        _service = new TrainingCalendarEventService(_db);
    }

    public async ValueTask DisposeAsync() => await _db.DisposeAsync();

    [Theory]
    [InlineData(null)]
    [InlineData("America/Vancouver")]
    [InlineData("Pacific/Auckland")]
    public async Task GetEventsAsync_InclusiveBoundariesAndMultipleVersions_ProjectsEveryMatchingRecord(
        string? timeZoneId
    )
    {
        var first = await AddAsync(_userA, 1, 2026, 1, 1, 2026, 1, 3);
        var second = await AddAsync(_userA, 2, 2026, 1, 7, 2026, 1, 7);
        var third = await AddAsync(_userB, 1, 2026, 1, 5, 2026, 1, 5);
        await AddAsync(_userB, 2, 2025, 12, 30, 2025, 12, 31);
        await AddAsync(_userB, 3, 2026, 1, 8, 2026, 1, 8);

        var events = await _service.GetEventsAsync(
            Request(3, 7) with
            {
                TimeZoneId = timeZoneId,
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(new[] { first.Id, second.Id, third.Id }.Order(), events.Select(e => e.UserTrainingId).Order());
        var firstEvent = Assert.Single(events, e => e.UserTrainingId == first.Id);
        Assert.Equal("training.user-training." + first.Id, firstEvent.Id);
        Assert.Equal("training.user-training", firstEvent.Type);
        Assert.Equal("training", firstEvent.SourceModule);
        Assert.Equal("Training: FIRST-AID", firstEvent.Title);
        Assert.Equal(_userA, firstEvent.UserId);
        Assert.Equal(1, firstEvent.TrainingId);
        Assert.Equal(1, firstEvent.LocationId);
        Assert.Equal(Day(2026, 1, 1), firstEvent.Start);
        Assert.Equal(Day(2026, 1, 4), firstEvent.End);
        Assert.True(firstEvent.AllDay);
        Assert.True(firstEvent.IsReadOnly);
        var singleDay = Assert.Single(events, e => e.UserTrainingId == second.Id);
        Assert.Equal(Day(2026, 1, 7), singleDay.Start);
        Assert.Equal(Day(2026, 1, 8), singleDay.End);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    public async Task GetEventsAsync_LegacyMinimumEnd_UsesAwardedDay(int hour)
    {
        var legacy = await AddAsync(_userA, 1, Day(2026, 1, 5).AddHours(hour), DateTimeOffset.MinValue, allDay: true);

        var result = await _service.GetEventsAsync(Request(5, 5), TestContext.Current.CancellationToken);

        var item = Assert.Single(result);
        Assert.Equal(legacy.Id, item.UserTrainingId);
        Assert.Equal(Day(2026, 1, 5), item.Start);
        Assert.Equal(Day(2026, 1, 6), item.End);
        Assert.True(item.AllDay);
        Assert.Empty(await _service.GetEventsAsync(Request(4, 4), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("2026-01-05T23:30:00-08:00", "2026-01-06T00:30:00-08:00", "America/Vancouver", "2026-01-05", true)]
    [InlineData("2026-01-05T23:30:00-08:00", "2026-01-06T00:30:00-08:00", "America/Vancouver", "2026-01-06", true)]
    [InlineData("2026-01-05T23:30:00-08:00", "2026-01-06T00:30:00-08:00", "America/Vancouver", "2026-01-07", false)]
    [InlineData("2026-01-05T23:30:00-08:00", "2026-01-06T00:00:00-08:00", "America/Vancouver", "2026-01-06", false)]
    [InlineData("2026-01-06T00:00:00-08:00", "2026-01-06T01:00:00-08:00", "America/Vancouver", "2026-01-05", false)]
    [InlineData("2026-01-05T15:30:00-08:00", "2026-01-05T16:30:00-08:00", "America/Vancouver", "2026-01-05", true)]
    [InlineData("2026-01-05T15:30:00-08:00", "2026-01-05T16:30:00-08:00", "America/Vancouver", "2026-01-06", false)]
    [InlineData("2026-01-05T00:00:00-08:00", "2026-01-06T00:00:00-08:00", "America/Vancouver", "2026-01-06", false)]
    [InlineData("2026-01-05T00:30:00+13:00", "2026-01-05T01:30:00+13:00", "Pacific/Auckland", "2026-01-05", true)]
    [InlineData("2026-01-06T00:30:00Z", "2026-01-06T01:30:00Z", null, "2026-01-05", false)]
    [InlineData("2026-01-06T00:30:00Z", "2026-01-06T01:30:00Z", null, "2026-01-06", true)]
    [InlineData("2026-01-05T00:00:00Z", "2026-01-06T00:00:00Z", "UTC", "2026-01-06", false)]
    [InlineData("2026-01-05T00:00:00Z", "2026-01-06T00:00:00Z", "UTC", "2026-01-05", true)]
    [InlineData("2025-03-09T01:30:00-08:00", "2025-03-09T03:30:00-07:00", "America/Vancouver", "2025-03-09", true)]
    [InlineData("2025-03-09T23:30:00-07:00", "2025-03-10T00:30:00-07:00", "America/Vancouver", "2025-03-09", true)]
    [InlineData("2025-03-10T00:00:00-07:00", "2025-03-10T01:00:00-07:00", "America/Vancouver", "2025-03-09", false)]
    [InlineData("2025-11-02T01:30:00-07:00", "2025-11-02T01:30:00-08:00", "America/Vancouver", "2025-11-02", true)]
    [InlineData("2025-11-02T23:30:00-08:00", "2025-11-03T00:30:00-08:00", "America/Vancouver", "2025-11-02", true)]
    [InlineData("2025-11-03T00:00:00-08:00", "2025-11-03T01:00:00-08:00", "America/Vancouver", "2025-11-02", false)]
    [InlineData("2018-11-04T03:00:00Z", "2018-11-04T03:30:00Z", "America/Sao_Paulo", "2018-11-04", true)]
    [InlineData("2018-11-04T02:30:00Z", "2018-11-04T02:59:59Z", "America/Sao_Paulo", "2018-11-04", false)]
    [InlineData("2018-11-04T03:00:00Z", "2018-11-04T03:30:00Z", "America/Sao_Paulo", "2018-11-03", false)]
    [InlineData("2025-11-02T04:00:00Z", "2025-11-02T04:30:00Z", "America/Havana", "2025-11-02", true)]
    [InlineData("2025-11-02T04:30:00Z", "2025-11-02T05:00:00Z", "America/Havana", "2025-11-02", true)]
    [InlineData("2025-11-02T03:30:00Z", "2025-11-02T04:00:00Z", "America/Havana", "2025-11-02", false)]
    [InlineData("2025-11-02T04:00:00Z", "2025-11-02T04:30:00Z", "America/Havana", "2025-11-01", false)]
    public async Task GetEventsAsync_TimedTraining_UsesLocalHalfOpenRangeAndPreservesTimestamps(
        string start,
        string end,
        string? timeZoneId,
        string queryDate,
        bool expected
    )
    {
        var startAtUtc = DateTimeOffset.Parse(start, CultureInfo.InvariantCulture).ToUniversalTime();
        var endAtUtc = DateTimeOffset.Parse(end, CultureInfo.InvariantCulture).ToUniversalTime();
        var training = await AddAsync(_userA, 1, startAtUtc, endAtUtc);
        var day = DateOnly.Parse(queryDate, CultureInfo.InvariantCulture);
        var events = await _service.GetEventsAsync(
            new TrainingCalendarEventsRequest
            {
                StartDate = day,
                EndDate = day,
                TimeZoneId = timeZoneId,
                UserIds = [_userA],
            },
            TestContext.Current.CancellationToken
        );

        if (!expected)
        {
            Assert.Empty(events);
            return;
        }

        var item = Assert.Single(events);
        Assert.Equal($"training.user-training.{training.Id}", item.Id);
        Assert.Equal(startAtUtc, item.Start);
        Assert.Equal(endAtUtc, item.End);
        Assert.False(item.AllDay);
        Assert.True(item.IsReadOnly);
        Assert.Equal("Training: FIRST-AID", item.Title);
        Assert.Equal(_userA, item.UserId);
        Assert.Equal(1, item.LocationId);
    }

    [Fact]
    public async Task GetEventsAsync_InvalidMidnight_UsesFirstValidInstantWithExclusiveEnd()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        var boundary = new DateTimeOffset(2018, 11, 4, 3, 0, 0, TimeSpan.Zero);
        // Windows historical rules place this transition one millisecond earlier than IANA data.
        if (TimeZoneInfo.ConvertTime(boundary.AddMilliseconds(-1), zone).Day == 4)
            boundary = boundary.AddMilliseconds(-1);
        await AddAsync(_userA, 1, boundary.AddHours(-1), boundary);
        var included = await AddAsync(_userA, 2, boundary, boundary.AddMinutes(1));

        var events = await _service.GetEventsAsync(
            new TrainingCalendarEventsRequest
            {
                StartDate = new DateOnly(2018, 11, 4),
                EndDate = new DateOnly(2018, 11, 4),
                TimeZoneId = zone.Id,
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(included.Id, Assert.Single(events).UserTrainingId);
    }

    [Fact]
    public async Task GetEventsAsync_MaximumStoredEnd_CapsEventAtRequestedExclusiveEnd()
    {
        var longRunning = await AddAsync(_userA, 1, 2026, 1, 1, 9999, 12, 31);
        var ordinary = await AddAsync(_userB, 1, 2026, 1, 6, 2026, 1, 6);

        var events = await _service.GetEventsAsync(Request(5, 7), TestContext.Current.CancellationToken);

        Assert.Equal(2, events.Count);
        var longRunningEvent = Assert.Single(events, e => e.UserTrainingId == longRunning.Id);
        Assert.Equal(Day(2026, 1, 1), longRunningEvent.Start);
        Assert.Equal(Day(2026, 1, 8), longRunningEvent.End);
        Assert.Equal(Day(2026, 1, 7), Assert.Single(events, e => e.UserTrainingId == ordinary.Id).End);
    }

    [Fact]
    public async Task GetEventsAsync_ExplicitUserFromAnotherLocation_IsIncludedWithoutLocationPeers()
    {
        var included = await AddAsync(_userA, 1, 2026, 1, 5, 2026, 1, 5);
        await AddAsync(_userB, 1, 2026, 1, 5, 2026, 1, 5);
        await AddAsync(_userC, 1, 2026, 1, 5, 2026, 1, 5);

        var result = await _service.GetEventsAsync(
            Request(5, 5) with
            {
                LocationId = 2,
                UserIds = [_userA],
            },
            TestContext.Current.CancellationToken
        );

        var item = Assert.Single(result);
        Assert.Equal(included.Id, item.UserTrainingId);
        Assert.Equal(1, item.LocationId);
    }

    [Fact]
    public async Task GetEventsAsync_ExplicitUsersFromDifferentLocations_ExcludesUnselectedLocationPeers()
    {
        var first = await AddAsync(_userA, 1, 2026, 1, 5, 2026, 1, 5);
        var second = await AddAsync(_userB, 1, 2026, 1, 5, 2026, 1, 5);
        await AddAsync(_userC, 1, 2026, 1, 5, 2026, 1, 5);

        var result = await _service.GetEventsAsync(
            Request(5, 5) with
            {
                LocationId = 2,
                UserIds = [_userA, _userB],
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal([first.Id, second.Id], result.Select(item => item.UserTrainingId));
        Assert.Equal([1, 2], result.Select(item => item.LocationId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetEventsAsync_NullOrEmptyUsers_FiltersByHomeLocation(bool emptyUsers)
    {
        await AddAsync(_userA, 1, 2026, 1, 5, 2026, 1, 5);
        var first = await AddAsync(_userB, 1, 2026, 1, 5, 2026, 1, 5);
        var second = await AddAsync(_userC, 1, 2026, 1, 5, 2026, 1, 5);

        var result = await _service.GetEventsAsync(
            Request(5, 5) with
            {
                LocationId = 2,
                UserIds = emptyUsers ? [] : null,
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal([first.Id, second.Id], result.Select(item => item.UserTrainingId));
        Assert.All(result, item => Assert.Equal(2, item.LocationId));
    }

    [Fact]
    public async Task GetEventsAsync_NoUserOrLocationFilter_ReturnsAllUsers()
    {
        var first = await AddAsync(_userA, 1, 2026, 1, 5, 2026, 1, 5);
        var second = await AddAsync(_userB, 1, 2026, 1, 5, 2026, 1, 5);
        var third = await AddAsync(_userC, 1, 2026, 1, 5, 2026, 1, 5);

        var result = await _service.GetEventsAsync(Request(5, 5), TestContext.Current.CancellationToken);

        Assert.Equal([first.Id, second.Id, third.Id], result.Select(item => item.UserTrainingId));
    }

    private Task<UserTraining> AddAsync(
        Guid userId,
        int version,
        int startYear,
        int startMonth,
        int startDay,
        int endYear,
        int endMonth,
        int endDay,
        bool legacyEnd = false
    ) =>
        AddAsync(
            userId,
            version,
            Day(startYear, startMonth, startDay),
            legacyEnd ? DateTimeOffset.MinValue : Day(endYear, endMonth, endDay),
            allDay: true
        );

    private async Task<UserTraining> AddAsync(
        Guid userId,
        int version,
        DateTimeOffset awardedOn,
        DateTimeOffset endingOn,
        bool allDay = false
    )
    {
        var entity = new UserTraining
        {
            UserId = userId,
            TrainingId = 1,
            Version = version,
            AwardedOn = awardedOn,
            EndingOn = endingOn,
            AllDay = allDay,
        };
        _db.UserTrainings.Add(entity);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return entity;
    }

    private static TrainingCalendarEventsRequest Request(int startDay, int endDay) =>
        new() { StartDate = new DateOnly(2026, 1, startDay), EndDate = new DateOnly(2026, 1, endDay) };

    private static DateTimeOffset Day(int year, int month, int day) => new(year, month, day, 0, 0, 0, TimeSpan.Zero);
}
