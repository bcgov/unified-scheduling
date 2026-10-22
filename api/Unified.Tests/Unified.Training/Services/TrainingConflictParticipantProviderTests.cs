using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Unified.Calendar.Options;
using Unified.Calendar.Services;
using Unified.Common.Calendar.Conflicts;
using Unified.Common.Time;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Training;
using Unified.Db.Models.UserManagement;
using Unified.Tests.TestHelpers;
using Unified.Training.Services;

namespace Unified.Tests.Training.Services;

public sealed class TrainingConflictParticipantProviderTests : IAsyncLifetime
{
    private readonly Guid _userA = Guid.NewGuid();
    private readonly Guid _userB = Guid.NewGuid();
    private UnifiedDbContext _db = null!;
    private TrainingConflictParticipantProvider _provider = null!;

    public async ValueTask InitializeAsync()
    {
        _db = new SqliteTestUnifiedDbContext(
            new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite("Data Source=:memory:").Options
        );
        await _db.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await _db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        _db.Locations.Add(
            new Location
            {
                Id = 1,
                AgencyId = "A1",
                Name = "Vancouver",
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
                HomeLocationId = 1,
            }
        );
        _db.Trainings.Add(
            new global::Unified.Db.Models.Training.Training
            {
                Id = 1,
                Code = "FIRST-AID",
                Description = "Course",
            }
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var timeZoneService = new TimeZoneService();
        _provider = new TrainingConflictParticipantProvider(
            _db,
            new CalendarTimeZoneResolver(
                Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "UTC" }),
                timeZoneService
            ),
            timeZoneService
        );
    }

    public async ValueTask DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task GetParticipantsAsync_MultipleVersionsAndLegacyEnd_UsesStableIdentityAndResource()
    {
        var first = await AddAsync(_userA, 1, Day(2026, 10, 21), Day(2026, 10, 23));
        var second = await AddAsync(_userA, 2, Day(2026, 10, 22), DateTimeOffset.MinValue);
        await AddAsync(_userB, 1, Day(2026, 10, 22), Day(2026, 10, 22));

        var participants = await _provider.GetParticipantsAsync(
            new CalendarConflictQuery(Day(2026, 10, 23), Day(2026, 10, 24), [_userA], IncludeDraftParticipants: false),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(2, participants.Count);
        Assert.Contains(
            participants,
            participant => participant.EventId == TrainingConflictParticipantProvider.IdentityFor(first.Id).EventId
        );
        var legacy = Assert.Single(participants, participant => participant.SourceEntityId == second.Id);
        Assert.Equal("training", legacy.SourceModule);
        Assert.Equal("Training: FIRST-AID", legacy.Title);
        var projectedTitle = Assert
            .Single(
                (
                    await new TrainingCalendarEventService(_db).GetEventsAsync(
                        new global::Unified.Training.Models.Calendar.TrainingCalendarEventsRequest
                        {
                            StartDate = new DateOnly(2026, 10, 22),
                            EndDate = new DateOnly(2026, 10, 22),
                            UserIds = [_userA],
                        },
                        TestContext.Current.CancellationToken
                    )
                ).Where(item => item.UserTrainingId == second.Id)
            )
            .Title;
        Assert.Equal(projectedTitle, legacy.Title);
        Assert.Equal(_userA, legacy.ResourceId);
        Assert.Equal(new DateTimeOffset(2026, 10, 22, 7, 0, 0, TimeSpan.Zero), legacy.Start);
        Assert.Equal(new DateTimeOffset(2026, 10, 23, 7, 0, 0, TimeSpan.Zero), legacy.End);
        Assert.Equal("America/Vancouver", legacy.TimeZoneId);
        Assert.Null(
            await _provider.GetParticipantAsync(legacy.Identity, _userB, TestContext.Current.CancellationToken)
        );
        Assert.Equal(
            legacy,
            await _provider.GetParticipantAsync(legacy.Identity, _userA, TestContext.Current.CancellationToken)
        );
    }

    [Theory]
    [InlineData(2026, 3, 8, 8, 7)]
    [InlineData(2026, 11, 1, 7, 8)]
    public async Task GetParticipantsAsync_DstDay_UsesLocalMidnights(
        int year,
        int month,
        int day,
        int startHour,
        int endHour
    )
    {
        await AddAsync(_userA, 1, Day(year, month, day), Day(year, month, day));

        var participant = Assert.Single(
            await _provider.GetParticipantsAsync(
                new CalendarConflictQuery(Day(year, month, day).AddDays(-1), Day(year, month, day).AddDays(2)),
                TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(new DateTimeOffset(year, month, day, startHour, 0, 0, TimeSpan.Zero), participant.Start);
        Assert.Equal(
            new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero).AddDays(1).AddHours(endHour),
            participant.End
        );
    }

    [Fact]
    public async Task GetParticipantsAsync_FinalDayLateEveningOverlapsButFollowingDayDoesNot()
    {
        await AddAsync(_userA, 1, Day(2026, 10, 21), Day(2026, 10, 23));
        var finalEvening = Day(2026, 10, 24).AddHours(6);
        var followingDay = Day(2026, 10, 24).AddHours(7);

        Assert.Single(
            await _provider.GetParticipantsAsync(
                new CalendarConflictQuery(finalEvening, finalEvening.AddMinutes(30), [_userA]),
                TestContext.Current.CancellationToken
            )
        );
        Assert.Empty(
            await _provider.GetParticipantsAsync(
                new CalendarConflictQuery(followingDay, followingDay.AddMinutes(30), [_userA]),
                TestContext.Current.CancellationToken
            )
        );
        Assert.Empty(
            await _provider.GetParticipantsAsync(
                new CalendarConflictQuery(finalEvening, finalEvening.AddMinutes(30), [_userB]),
                TestContext.Current.CancellationToken
            )
        );
    }

    private async Task<UserTraining> AddAsync(
        Guid userId,
        int version,
        DateTimeOffset awardedOn,
        DateTimeOffset endingOn
    )
    {
        var record = new UserTraining
        {
            UserId = userId,
            TrainingId = 1,
            Version = version,
            AwardedOn = awardedOn,
            EndingOn = endingOn,
            ExpiryDate = awardedOn.AddYears(2),
        };
        _db.UserTrainings.Add(record);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return record;
    }

    private static DateTimeOffset Day(int year, int month, int day) => new(year, month, day, 0, 0, 0, TimeSpan.Zero);
}
