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

    [Fact]
    public async Task GetEventsAsync_InclusiveBoundariesAndMultipleVersions_ProjectsEveryMatchingRecord()
    {
        var first = await AddAsync(_userA, 1, 2026, 1, 1, 2026, 1, 3);
        var second = await AddAsync(_userA, 2, 2026, 1, 7, 2026, 1, 7);
        var third = await AddAsync(_userB, 1, 2026, 1, 5, 2026, 1, 5);
        await AddAsync(_userB, 2, 2025, 12, 30, 2025, 12, 31);
        await AddAsync(_userB, 3, 2026, 1, 8, 2026, 1, 8);

        var events = await _service.GetEventsAsync(Request(3, 7), TestContext.Current.CancellationToken);

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

    [Fact]
    public async Task GetEventsAsync_LegacyMinimumEnd_UsesAwardedDay()
    {
        var legacy = await AddAsync(_userA, 1, 2026, 1, 5, 2026, 1, 5, legacyEnd: true);

        var result = await _service.GetEventsAsync(Request(5, 5), TestContext.Current.CancellationToken);

        var item = Assert.Single(result);
        Assert.Equal(legacy.Id, item.UserTrainingId);
        Assert.Equal(Day(2026, 1, 5), item.Start);
        Assert.Equal(Day(2026, 1, 6), item.End);
        Assert.Empty(await _service.GetEventsAsync(Request(4, 4), TestContext.Current.CancellationToken));
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
    public async Task GetEventsAsync_LocationAndUserFilters_AreCombined()
    {
        var included = await AddAsync(_userA, 1, 2026, 1, 5, 2026, 1, 5);
        await AddAsync(_userB, 1, 2026, 1, 5, 2026, 1, 5);

        var result = await _service.GetEventsAsync(
            Request(5, 5) with
            {
                LocationId = 1,
                UserIds = [_userA, _userB],
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(included.Id, Assert.Single(result).UserTrainingId);
        Assert.Empty(
            await _service.GetEventsAsync(
                Request(5, 5) with
                {
                    LocationId = 2,
                    UserIds = [_userA],
                },
                TestContext.Current.CancellationToken
            )
        );
    }

    private async Task<UserTraining> AddAsync(
        Guid userId,
        int version,
        int startYear,
        int startMonth,
        int startDay,
        int endYear,
        int endMonth,
        int endDay,
        bool legacyEnd = false
    )
    {
        var entity = new UserTraining
        {
            UserId = userId,
            TrainingId = 1,
            Version = version,
            AwardedOn = Day(startYear, startMonth, startDay),
            EndingOn = legacyEnd ? DateTimeOffset.MinValue : Day(endYear, endMonth, endDay),
        };
        _db.UserTrainings.Add(entity);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return entity;
    }

    private static TrainingCalendarEventsRequest Request(int startDay, int endDay) =>
        new() { StartDate = new DateOnly(2026, 1, startDay), EndDate = new DateOnly(2026, 1, endDay) };

    private static DateTimeOffset Day(int year, int month, int day) => new(year, month, day, 0, 0, 0, TimeSpan.Zero);
}
