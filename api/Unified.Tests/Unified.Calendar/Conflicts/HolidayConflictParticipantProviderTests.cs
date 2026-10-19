using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.CreateFunction("now", () => DateTimeOffset.UtcNow.ToString("O"));
        await _connection.OpenAsync(TestContext.Current.CancellationToken);
        _db = new SqliteTestUnifiedDbContext(
            new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite(_connection).Options
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
        Assert.Equal(identity, participant!.Identity);
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
                        && (query.ResourceIds is not { Count: > 0 } || query.ResourceIds.Contains(participant.ResourceId))
                    )
                    .ToList()
            );
        }
    }
}
