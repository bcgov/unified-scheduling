using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Unified.Authorization;
using Unified.Calendar.Conflicts;
using Unified.Calendar.Holidays;
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

    [Theory]
    [InlineData("2026-10-22T09:00:00-07:00", "2026-10-22T10:00:00-07:00", "2026-10-22T14:00:00-07:00", false)]
    [InlineData("2026-10-22T09:00:00-07:00", "2026-10-22T10:00:00-07:00", "2026-10-22T09:30:00-07:00", true)]
    [InlineData("2026-10-22T09:00:00-07:00", "2026-10-22T10:00:00-07:00", "2026-10-22T10:00:00-07:00", false)]
    [InlineData("2026-10-22T09:00:00-07:00", "2026-10-22T10:00:00-07:00", "2026-10-22T08:00:00-07:00", false)]
    [InlineData("2026-10-22T23:30:00-07:00", "2026-10-23T00:30:00-07:00", "2026-10-23T00:00:00-07:00", true)]
    [InlineData("2026-10-22T00:00:00Z", "2026-10-22T01:00:00Z", "2026-10-22T02:00:00Z", false)]
    [InlineData("2026-10-21T23:00:00Z", "2026-10-22T00:00:00Z", "2026-10-22T00:00:00Z", false)]
    [InlineData("2025-03-09T01:30:00-08:00", "2025-03-09T03:30:00-07:00", "2025-03-09T03:00:00-07:00", true)]
    [InlineData("2025-03-09T01:30:00-08:00", "2025-03-09T03:30:00-07:00", "2025-03-09T03:30:00-07:00", false)]
    [InlineData("2025-11-02T01:30:00-07:00", "2025-11-02T01:30:00-08:00", "2025-11-02T01:00:00-08:00", true)]
    [InlineData("2025-11-02T01:00:00-07:00", "2025-11-02T01:30:00-07:00", "2025-11-02T01:00:00-08:00", false)]
    public async Task GetParticipantsAsync_TimedTraining_UsesPreciseOverlapAndMatchesIndividual(
        string start,
        string end,
        string assignmentStart,
        bool expectedConflict
    )
    {
        var startAtUtc = DateTimeOffset.Parse(start, CultureInfo.InvariantCulture).ToUniversalTime();
        var endAtUtc = DateTimeOffset.Parse(end, CultureInfo.InvariantCulture).ToUniversalTime();
        var training = await AddAsync(_userA, 1, startAtUtc, endAtUtc);
        var identity = TrainingConflictParticipantProvider.IdentityFor(training.Id);
        var individual = await _provider.GetParticipantAsync(identity, _userA, TestContext.Current.CancellationToken);
        var participant = Assert.Single(
            await _provider.GetParticipantsAsync(
                new CalendarConflictQuery(startAtUtc, endAtUtc, [_userA]),
                TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(individual, participant);
        Assert.Equal(startAtUtc, participant.Start);
        Assert.Equal(endAtUtc, participant.End);
        Assert.False(participant.AllDay);
        Assert.Equal("America/Vancouver", participant.TimeZoneId);
        Assert.Null(await _provider.GetParticipantAsync(identity, _userB, TestContext.Current.CancellationToken));
        var assignmentStartAtUtc = DateTimeOffset
            .Parse(assignmentStart, CultureInfo.InvariantCulture)
            .ToUniversalTime();
        var assignment = new CalendarConflictParticipant(
            "assignment.1",
            "scheduling",
            _userA,
            assignmentStartAtUtc,
            assignmentStartAtUtc.AddHours(1),
            "Assignment",
            EventTypeCode: "assignment"
        );
        Assert.Equal(expectedConflict ? 1 : 0, CalendarConflictDetector.Detect([participant, assignment]).Count);
        Assert.Equal(
            expectedConflict ? 1 : 0,
            (
                await _provider.GetParticipantsAsync(
                    new CalendarConflictQuery(assignment.Start, assignment.End, [_userA]),
                    TestContext.Current.CancellationToken
                )
            ).Count
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetParticipantsAsync_Training_PreservesPermissionAwareWarnings(bool legacyEnd)
    {
        var start = Day(2026, 10, 22).AddHours(16);
        var training = await AddAsync(_userA, 1, start, legacyEnd ? DateTimeOffset.MinValue : start.AddHours(1));
        var participant = Assert.Single(
            await _provider.GetParticipantsAsync(
                new CalendarConflictQuery(Day(2026, 10, 22), Day(2026, 10, 23), [_userA]),
                TestContext.Current.CancellationToken
            )
        );
        Assert.Equal(TrainingConflictParticipantProvider.IdentityFor(training.Id), participant.Identity);
        Assert.Equal(legacyEnd, participant.AllDay);
        Assert.Equal(legacyEnd ? Day(2026, 10, 22).AddHours(7) : start, participant.Start);
        Assert.Equal(legacyEnd ? Day(2026, 10, 23).AddHours(7) : start.AddHours(1), participant.End);
        var assignment = participant with
        {
            EventId = "assignment.1",
            SourceModule = "scheduling",
            Title = "Restricted assignment",
            Start = start.AddMinutes(30),
            End = start.AddMinutes(90),
            EventTypeCode = "assignment",
        };
        var conflict = Assert.Single(CalendarConflictDetector.Detect([participant, assignment])) with
        {
            ReportOnEntry = true,
            ReportOnOverlaps = true,
        };
        var projection = new CalendarConflictProjection([_provider]);
        var result = await projection.ProjectAsync(
            [conflict],
            CalendarConflictTestContext.Viewer(_userA, nameof(Permissions.UserTrainingsView)),
            TestContext.Current.CancellationToken
        );
        Assert.Empty(result.Conflicts);
        Assert.Equal(participant.EventId, Assert.Single(result.RestrictedWarnings).EventId);
        var hidden = await projection.ProjectAsync(
            [conflict],
            CalendarConflictTestContext.Viewer(_userA),
            TestContext.Current.CancellationToken
        );
        Assert.Empty(hidden.Conflicts);
        Assert.Empty(hidden.RestrictedWarnings);
    }

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
        Assert.All(participants, participant => Assert.Equal("training", participant.EventTypeCode));
        Assert.Contains(
            participants,
            participant => participant.EventId == TrainingConflictParticipantProvider.IdentityFor(first.Id).EventId
        );
        var legacy = Assert.Single(participants, participant => participant.SourceEntityId == second.Id);
        Assert.Equal("training", legacy.SourceModule);
        Assert.True(legacy.AllDay);
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
                ),
                item => item.UserTrainingId == second.Id
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
    [InlineData(2025, 3, 9, 8, 7)]
    [InlineData(2025, 11, 2, 7, 8)]
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

    [Fact]
    public async Task GetParticipantsAsync_MaximumCalendarDate_ReturnsOverlappingTrainingWithoutOverflow()
    {
        await SetTimeZoneAsync("UTC");
        var training = await AddAsync(_userA, 1, Day(9999, 12, 29), Day(9999, 12, 30));
        await AddAsync(_userA, 2, Day(9999, 12, 27), Day(9999, 12, 28));
        var participant = Assert.Single(
            await _provider.GetParticipantsAsync(
                new CalendarConflictQuery(Day(9999, 12, 30), Day(9999, 12, 31), [_userA]),
                TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(training.Id, participant.SourceEntityId);
        Assert.Equal(Day(9999, 12, 29), participant.Start);
        Assert.Equal(Day(9999, 12, 31), participant.End);
    }

    [Fact]
    public async Task GetParticipantsAsync_MinimumInstant_ReturnsOverlappingTrainingWithoutOverflow()
    {
        await SetTimeZoneAsync("UTC");
        var training = await AddAsync(_userA, 1, DateTimeOffset.MinValue, DateTimeOffset.MinValue);
        await AddAsync(_userA, 2, Day(1, 1, 2), Day(1, 1, 2));
        var participant = Assert.Single(
            await _provider.GetParticipantsAsync(
                new CalendarConflictQuery(DateTimeOffset.MinValue, Day(1, 1, 2), [_userA]),
                TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(training.Id, participant.SourceEntityId);
        Assert.Equal(DateTimeOffset.MinValue, participant.Start);
        Assert.Equal(Day(1, 1, 2), participant.End);
    }

    [Theory]
    [InlineData("UTC", 30, false)]
    [InlineData("America/Vancouver", 30, false)]
    [InlineData("America/Edmonton", 31, false)]
    [InlineData("America/Vancouver", 31, true)]
    public async Task GetParticipantsAsync_MaximumStoredEnd_MatchesIndividualParticipant(
        string timeZoneId,
        int startDay,
        bool legacyEnd
    )
    {
        await SetTimeZoneAsync(timeZoneId);
        var training = await AddAsync(
            _userA,
            1,
            Day(9999, 12, startDay),
            legacyEnd ? DateTimeOffset.MinValue : DateTimeOffset.MaxValue
        );
        var identity = TrainingConflictParticipantProvider.IdentityFor(training.Id);
        var individual = Assert.IsType<CalendarConflictParticipant>(
            await _provider.GetParticipantAsync(identity, _userA, TestContext.Current.CancellationToken)
        );
        var participant = Assert.Single(
            await _provider.GetParticipantsAsync(
                new CalendarConflictQuery(individual.Start, DateTimeOffset.MaxValue, [_userA]),
                TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(individual, participant);
        Assert.Equal(identity, participant.Identity);
        Assert.Equal(_userA, participant.ResourceId);
        Assert.Equal(TimeZoneService.NormalizeTimeZoneId(timeZoneId), participant.TimeZoneId);
        Assert.Equal(DateTimeOffset.MaxValue, participant.End);
        var timeZoneService = new TimeZoneService();
        // MaxValue has a time component, so only the legacy missing-end record is all-day.
        Assert.Equal(legacyEnd, participant.AllDay);
        var expectedStart = legacyEnd
            ? timeZoneService.ToUtcInstant(
                new DateTime(9999, 12, startDay),
                timeZoneService.ResolveRequired(timeZoneId)
            )
            : Day(9999, 12, startDay);
        Assert.Equal(expectedStart, participant.Start);
    }

    private async Task SetTimeZoneAsync(string timeZoneId)
    {
        var location = await _db.Locations.SingleAsync(TestContext.Current.CancellationToken);
        location.Timezone = timeZoneId;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetParticipantAsync_NullHomeLocation_UsesConfiguredTimeZoneFallback()
    {
        var user = await _db.Users.SingleAsync(user => user.Id == _userA, TestContext.Current.CancellationToken);
        user.HomeLocationId = null;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var training = await AddAsync(_userA, 1, Day(2026, 7, 13), DateTimeOffset.MinValue);

        var participant = Assert.IsType<CalendarConflictParticipant>(
            await _provider.GetParticipantAsync(
                TrainingConflictParticipantProvider.IdentityFor(training.Id),
                _userA,
                TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(TimeZoneService.NormalizeTimeZoneId("UTC"), participant.TimeZoneId);
        Assert.Equal(Day(2026, 7, 13), participant.Start);
        Assert.Equal(Day(2026, 7, 14), participant.End);
    }

    [Fact]
    public async Task GetConflictsAsync_TrainingReportsHoliday_DoesNotInferReciprocalReportingOrBlocking()
    {
        var user = await _db.Users.SingleAsync(user => user.Id == _userA, TestContext.Current.CancellationToken);
        user.IsEnabled = true;
        await AddAsync(_userA, 1, Day(2026, 7, 1), Day(2026, 7, 1));
        var timeZoneService = new TimeZoneService();
        var resolver = new CalendarTimeZoneResolver(
            Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "UTC" }),
            timeZoneService
        );
        var holidayProvider = new HolidayConflictParticipantProvider(
            _db,
            new StatutoryHolidayCalendarDataProvider(new BcStatutoryHolidayCalculator(), timeZoneService),
            resolver,
            timeZoneService
        );
        var service = new CalendarConflictService(
            [_provider, holidayProvider],
            _db,
            new CalendarConflictProjection([]),
            CalendarConflictTestRules.Create(("training", ["holiday"]), ("holiday", []))
        );
        var query = new CalendarConflictQuery(Day(2026, 7, 1), Day(2026, 7, 3), [_userA]);

        var conflict = Assert.Single(await service.GetConflictsAsync(query, TestContext.Current.CancellationToken));

        Assert.Equal("holiday", conflict.Entry.EventTypeCode);
        Assert.Equal("training", conflict.Overlaps.EventTypeCode);
        Assert.False(conflict.ReportOnEntry);
        Assert.True(conflict.ReportOnOverlaps);
        Assert.Empty(
            await service.GetConflictsForCandidatesAsync([conflict.Entry], TestContext.Current.CancellationToken)
        );
        await Assert.ThrowsAsync<CalendarConflictException>(() =>
            service.EnsureNoUnresolvedConflictsAsync([conflict.Overlaps], TestContext.Current.CancellationToken)
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
            ExpiryDate = awardedOn.Year <= 9997 ? awardedOn.AddYears(2) : null,
        };
        _db.UserTrainings.Add(record);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return record;
    }

    private static DateTimeOffset Day(int year, int month, int day) => new(year, month, day, 0, 0, 0, TimeSpan.Zero);
}
