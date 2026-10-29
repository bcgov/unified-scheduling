using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Unified.Calendar.Conflicts;
using Unified.Calendar.Holidays;
using Unified.Calendar.Options;
using Unified.Calendar.Services;
using Unified.Common.Calendar.Conflicts;
using Unified.Common.Time;
using Unified.Db;
using Unified.Db.Models.UserManagement;
using Unified.Tests.TestHelpers;

namespace Unified.Tests.Calendar.Conflicts;

public sealed class HolidayConflictParticipantProviderTests : IAsyncLifetime
{
    private static readonly Guid FirstEnabledUserId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondEnabledUserId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid DisabledUserId = new("33333333-3333-3333-3333-333333333333");

    private SqliteConnection _connection = null!;
    private UnifiedDbContext _db = null!;
    private HolidayConflictParticipantProvider _provider = null!;
    private readonly UserQueryCounter _userQueryCounter = new();

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.CreateFunction("now", () => DateTimeOffset.UtcNow.ToString("O"));
        await _connection.OpenAsync(TestContext.Current.CancellationToken);
        _db = new SqliteTestUnifiedDbContext(
            new DbContextOptionsBuilder<UnifiedDbContext>()
                .UseSqlite(_connection)
                .AddInterceptors(_userQueryCounter)
                .Options
        );
        await _db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        _db.Users.AddRange(
            CreateUser(FirstEnabledUserId, isEnabled: true),
            CreateUser(SecondEnabledUserId, isEnabled: true),
            CreateUser(DisabledUserId, isEnabled: false)
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var timeZoneService = new TimeZoneService();
        var timeZoneResolver = new CalendarTimeZoneResolver(
            Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "America/Vancouver" }),
            timeZoneService
        );
        _provider = new HolidayConflictParticipantProvider(
            _db,
            new StatutoryHolidayCalendarDataProvider(new BcStatutoryHolidayCalculator(), timeZoneService),
            timeZoneResolver,
            timeZoneService
        );
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task GetParticipantsAsync_WhenHolidayOverlaps_ReturnsEveryEnabledUserInRequestedTimeZone()
    {
        var participants = await _provider.GetParticipantsAsync(
            new CalendarConflictQuery(
                new DateTimeOffset(2026, 7, 1, 4, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 7, 2, 4, 0, 0, TimeSpan.Zero),
                TimeZoneIds: ["America/Toronto"]
            ),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(2, participants.Count);
        Assert.Equal(
            new[] { FirstEnabledUserId, SecondEnabledUserId },
            participants.Select(item => item.ResourceId).Order().ToArray()
        );
        Assert.All(
            participants,
            participant =>
            {
                Assert.Equal("stat-holiday:CanadaDay:2026-07-01:America/Toronto", participant.EventId);
                Assert.Equal("calendar", participant.SourceModule);
                Assert.Equal("Canada Day", participant.Title);
                Assert.Equal(new DateTimeOffset(2026, 7, 1, 4, 0, 0, TimeSpan.Zero), participant.Start);
                Assert.Equal(new DateTimeOffset(2026, 7, 2, 4, 0, 0, TimeSpan.Zero), participant.End);
                Assert.Equal("America/Toronto", participant.TimeZoneId);
                Assert.Equal("stat-holiday:CanadaDay:2026-07-01", participant.CanonicalEventId);
                Assert.Null(participant.SourceEntityId);
            }
        );
        Assert.DoesNotContain(participants, participant => participant.ResourceId == DisabledUserId);
    }

    [Fact]
    public async Task GetParticipantAsync_WhenGivenGeneratedIdentity_ReconstructsHolidayAndRejectsDisabledUser()
    {
        var identity = new CalendarConflictEventIdentity(
            "calendar",
            "stat-holiday:CanadaDay:2026-07-01:America/Vancouver"
        );

        var participant = await _provider.GetParticipantAsync(
            identity,
            FirstEnabledUserId,
            TestContext.Current.CancellationToken
        );
        var disabledParticipant = await _provider.GetParticipantAsync(
            identity,
            DisabledUserId,
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(participant);
        Assert.Equal(identity, participant.Identity);
        Assert.Equal(FirstEnabledUserId, participant.ResourceId);
        Assert.Equal(new DateTimeOffset(2026, 7, 1, 7, 0, 0, TimeSpan.Zero), participant.Start);
        Assert.Equal(new DateTimeOffset(2026, 7, 2, 7, 0, 0, TimeSpan.Zero), participant.End);
        Assert.Null(disabledParticipant);
    }

    [Fact]
    public async Task GetConflictsAsync_WhenAssignmentOverlapsHoliday_ReturnsCrossModuleConflict()
    {
        var assignment = new CalendarConflictParticipant(
            "42",
            "scheduling",
            FirstEnabledUserId,
            new DateTimeOffset(2026, 7, 1, 13, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 1, 14, 0, 0, TimeSpan.Zero),
            "Court Room Monitor",
            TimeZoneId: "America/Toronto"
        );
        var service = new CalendarConflictService([_provider, new FixedParticipantProvider(assignment)], _db);

        var conflict = Assert.Single(
            await service.GetConflictsAsync(
                new CalendarConflictQuery(
                    new DateTimeOffset(2026, 7, 1, 4, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 7, 2, 4, 0, 0, TimeSpan.Zero),
                    ResourceIds: [FirstEnabledUserId],
                    TimeZoneIds: ["America/Toronto"]
                ),
                TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(FirstEnabledUserId, conflict.ResourceId);
        Assert.Equal(
            new[] { "calendar", "scheduling" },
            new[] { conflict.Entry.SourceModule, conflict.Overlaps.SourceModule }
        );
        Assert.Contains("Canada Day", new[] { conflict.Entry.Title, conflict.Overlaps.Title });
    }

    [Fact]
    public async Task GetConflictsForCandidatesAsync_WhenCandidateTimeZonesDiffer_DoesNotApplyHolidayFromAnotherTimeZone()
    {
        var torontoAssignment = new CalendarConflictParticipant(
            "42",
            "scheduling",
            FirstEnabledUserId,
            new DateTimeOffset(2026, 7, 2, 4, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 2, 6, 0, 0, TimeSpan.Zero),
            "Toronto assignment",
            TimeZoneId: "America/Toronto"
        );
        var vancouverAssignment = new CalendarConflictParticipant(
            "43",
            "scheduling",
            SecondEnabledUserId,
            new DateTimeOffset(2026, 7, 2, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.Zero),
            "Vancouver assignment",
            TimeZoneId: "America/Vancouver"
        );
        var service = new CalendarConflictService([_provider], _db);

        var conflicts = await service.GetConflictsForCandidatesAsync(
            [torontoAssignment, vancouverAssignment],
            TestContext.Current.CancellationToken
        );

        Assert.Empty(conflicts);
    }

    [Fact]
    public async Task GetParticipantsAsync_WhenMultipleTimeZonesLackResourceContexts_Throws()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _provider.GetParticipantsAsync(
                new CalendarConflictQuery(
                    new DateTimeOffset(2026, 7, 1, 4, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 7, 2, 7, 0, 0, TimeSpan.Zero),
                    ResourceIds: [FirstEnabledUserId],
                    TimeZoneIds: ["America/Toronto", "America/Vancouver"]
                ),
                TestContext.Current.CancellationToken
            )
        );

        Assert.Contains("resource-specific", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetConflictsForCandidatesAsync_WhenResourceHasEventsInMultipleTimeZones_UsesEachEventContext()
    {
        var torontoAssignment = new CalendarConflictParticipant(
            "42",
            "scheduling",
            FirstEnabledUserId,
            new DateTimeOffset(2026, 7, 2, 4, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 2, 6, 0, 0, TimeSpan.Zero),
            "Toronto assignment",
            TimeZoneId: "America/Toronto"
        );
        var vancouverAssignment = new CalendarConflictParticipant(
            "43",
            "scheduling",
            FirstEnabledUserId,
            new DateTimeOffset(2026, 7, 1, 18, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 1, 19, 0, 0, TimeSpan.Zero),
            "Vancouver assignment",
            TimeZoneId: "America/Vancouver"
        );
        var service = new CalendarConflictService([_provider], _db);

        var conflicts = await service.GetConflictsForCandidatesAsync(
            [torontoAssignment, vancouverAssignment],
            TestContext.Current.CancellationToken
        );

        var conflict = Assert.Single(conflicts);
        Assert.Contains("43", new[] { conflict.Entry.EventId, conflict.Overlaps.EventId });
        Assert.Contains(
            "stat-holiday:CanadaDay:2026-07-01:America/Vancouver",
            new[] { conflict.Entry.EventId, conflict.Overlaps.EventId }
        );
    }

    [Fact]
    public async Task GetParticipantsAsync_WhenRangeContainsNoHoliday_DoesNotQueryUsers()
    {
        _userQueryCounter.Reset();

        var participants = await _provider.GetParticipantsAsync(
            new CalendarConflictQuery(
                new DateTimeOffset(2026, 8, 24, 7, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 8, 25, 7, 0, 0, TimeSpan.Zero),
                TimeZoneIds: ["America/Vancouver"]
            ),
            TestContext.Current.CancellationToken
        );

        Assert.Empty(participants);
        Assert.Equal(0, _userQueryCounter.UserQueryCount);
    }

    [Fact]
    public async Task GetParticipantsAsync_WhenResourceContextTimeZoneIsBlank_Throws()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _provider.GetParticipantsAsync(
                new CalendarConflictQuery(
                    new DateTimeOffset(2026, 7, 1, 7, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 7, 2, 7, 0, 0, TimeSpan.Zero),
                    ResourceIds: [FirstEnabledUserId],
                    ResourceTimeZones: [new(FirstEnabledUserId, " ")]
                ),
                TestContext.Current.CancellationToken
            )
        );

        Assert.Contains("operational time zone", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetParticipantsAsync_WhenResourceContextTimeZoneIsInvalid_Throws()
    {
        await Assert.ThrowsAsync<TimeZoneNotFoundException>(() =>
            _provider.GetParticipantsAsync(
                new CalendarConflictQuery(
                    new DateTimeOffset(2026, 7, 1, 7, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 7, 2, 7, 0, 0, TimeSpan.Zero),
                    ResourceIds: [FirstEnabledUserId],
                    ResourceTimeZones: [new(FirstEnabledUserId, "Invalid/TimeZone")]
                ),
                TestContext.Current.CancellationToken
            )
        );
    }

    private static User CreateUser(Guid id, bool isEnabled) =>
        new()
        {
            Id = id,
            FirstName = "Test",
            LastName = "User",
            IdirName = $"user-{id:N}",
            Email = $"user-{id:N}@example.com",
            IsEnabled = isEnabled,
        };

    private sealed class FixedParticipantProvider(params CalendarConflictParticipant[] participants)
        : ICalendarConflictParticipantProvider
    {
        public Task<CalendarConflictParticipant?> GetParticipantAsync(
            CalendarConflictEventIdentity identity,
            Guid resourceId,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(
                participants.SingleOrDefault(participant =>
                    participant.Identity == identity && participant.ResourceId == resourceId
                )
            );
        }

        public Task<IReadOnlyCollection<CalendarConflictParticipant>> GetParticipantsAsync(
            CalendarConflictQuery query,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyCollection<CalendarConflictParticipant>>(
                participants
                    .Where(participant =>
                        participant.Start < query.EndAtUtc
                        && participant.End > query.StartAtUtc
                        && (
                            query.ResourceIds is not { Count: > 0 }
                            || query.ResourceIds.Contains(participant.ResourceId)
                        )
                    )
                    .ToList()
            );
        }
    }

    private sealed class UserQueryCounter : DbCommandInterceptor
    {
        public int UserQueryCount { get; private set; }

        public void Reset() => UserQueryCount = 0;

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result
        )
        {
            CountUserQuery(command);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default
        )
        {
            CountUserQuery(command);
            return ValueTask.FromResult(result);
        }

        private void CountUserQuery(DbCommand command)
        {
            if (command.CommandText.Contains("FROM \"Users\"", StringComparison.OrdinalIgnoreCase))
                UserQueryCount++;
        }
    }
}
