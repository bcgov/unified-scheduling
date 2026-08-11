using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Unified.Calendar.Conflicts;
using Unified.Calendar.Models;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Lookup;
using Unified.Db.Models.UserManagement;
using Unified.Tests.TestHelpers;

namespace Unified.Tests.Calendar.Conflicts;

public sealed class CalendarConflictServiceTests : IAsyncLifetime
{
    private static readonly Guid ResourceId = new("11111111-1111-1111-1111-111111111111");
    private SqliteConnection _connection = null!;
    private UnifiedDbContext _db = null!;
    private MutableProvider _provider = null!;
    private CalendarConflictService _service = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.CreateFunction("now", () => DateTimeOffset.UtcNow.ToString("O"));
        await _connection.OpenAsync(TestContext.Current.CancellationToken);
        _db = new SqliteTestUnifiedDbContext(
            new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite(_connection).Options
        );
        await _db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        _db.EventTypes.Add(new EventType { Code = "assignment", Description = "Assignment" });
        _db.EventStatusTypes.Add(new EventStatusType { Code = "active", Description = "Active" });
        _db.Events.AddRange(CreateEvent(1, 8, 10), CreateEvent(2, 9, 11));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        _provider = new MutableProvider { Participants = [CreateParticipant(1, 8, 10), CreateParticipant(2, 9, 11)] };
        _service = new CalendarConflictService(new CalendarConflictDetector(), [_provider], _db);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task CreateOverrideAsync_NormalizesPairAndMarksRetrievedConflictOverridden()
    {
        var result = await _service.CreateOverrideAsync(
            new CalendarConflictOverrideRequest
            {
                FirstEventId = 2,
                SecondEventId = 1,
                Note = "Manager approved",
            },
            null,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(1, result.FirstEventId);
        Assert.Equal(2, result.SecondEventId);
        var conflict = Assert.Single(
            await _service.GetConflictsAsync(
                new CalendarConflictQuery(Baseline(7), Baseline(12)),
                TestContext.Current.CancellationToken
            )
        );
        Assert.True(conflict.IsOverridden);
        Assert.Equal("Manager approved", conflict.OverrideNote);
    }

    [Fact]
    public async Task InvalidateResolvedOverridesAsync_WhenPairNoLongerConflicts_DeactivatesOverride()
    {
        await _service.CreateOverrideAsync(
            new CalendarConflictOverrideRequest
            {
                FirstEventId = 1,
                SecondEventId = 2,
                Note = "Temporary",
            },
            null,
            TestContext.Current.CancellationToken
        );

        _provider.Participants = [CreateParticipant(1, 8, 10), CreateParticipant(2, 10, 11)];

        await _service.InvalidateResolvedOverridesAsync(
            [1],
            cancellationToken: TestContext.Current.CancellationToken
        );

        var persisted = await _db.CalendarConflictOverrides.SingleAsync(TestContext.Current.CancellationToken);
        Assert.False(persisted.IsActive);
        Assert.NotNull(persisted.InvalidatedOn);
    }

    [Fact]
    public async Task InvalidateResolvedOverridesAsync_WhenPairStillConflicts_PreservesOverride()
    {
        await _service.CreateOverrideAsync(
            new CalendarConflictOverrideRequest
            {
                FirstEventId = 1,
                SecondEventId = 2,
                Note = "Original state",
            },
            null,
            TestContext.Current.CancellationToken
        );
        _provider.Participants = [CreateParticipant(1, 8, 10), CreateParticipant(2, 9, 10, 30)];

        await _service.InvalidateResolvedOverridesAsync(
            [1],
            cancellationToken: TestContext.Current.CancellationToken
        );

        var conflict = Assert.Single(
            await _service.GetConflictsAsync(
                new CalendarConflictQuery(Baseline(7), Baseline(12)),
                TestContext.Current.CancellationToken
            )
        );

        Assert.True(conflict.IsOverridden);
        Assert.True(
            await _db.CalendarConflictOverrides
                .Select(overrideEntity => overrideEntity.IsActive)
                .SingleAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task CreateOverrideAsync_WhenOverrideIsUpdated_ReturnsStandardAuditFields()
    {
        var creatorId = Guid.NewGuid();
        var updaterId = Guid.NewGuid();
        _db.Users.AddRange(
            CreateUser(creatorId, "Alex", "Morgan", "amorgan"),
            CreateUser(updaterId, "Taylor", "Ng", "tng")
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _service.CreateOverrideAsync(
            new CalendarConflictOverrideRequest
            {
                FirstEventId = 1,
                SecondEventId = 2,
                Note = "Initial approval",
            },
            creatorId,
            TestContext.Current.CancellationToken
        );
        var updated = await _service.CreateOverrideAsync(
            new CalendarConflictOverrideRequest
            {
                FirstEventId = 1,
                SecondEventId = 2,
                Note = "Updated approval",
            },
            updaterId,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(creatorId, updated.CreatedById);
        Assert.Equal(updaterId, updated.UpdatedById);
        Assert.NotNull(updated.UpdatedOn);

        var conflict = Assert.Single(
            await _service.GetConflictsAsync(
                new CalendarConflictQuery(Baseline(7), Baseline(12)),
                TestContext.Current.CancellationToken
            )
        );
        Assert.Equal(creatorId, conflict.CreatedById);
        Assert.Equal(updaterId, conflict.UpdatedById);
        Assert.Equal(updated.UpdatedOn, conflict.UpdatedOn);
    }

    private static Event CreateEvent(int id, int startHour, int endHour) =>
        new()
        {
            Id = id,
            Title = $"Assignment {id}",
            StartAtUtc = Baseline(startHour),
            EndAtUtc = Baseline(endHour),
            EventTypeCode = "assignment",
            StatusTypeCode = "active",
            SourceModule = "scheduling",
        };

    private static User CreateUser(Guid id, string firstName, string lastName, string idirName) =>
        new()
        {
            Id = id,
            FirstName = firstName,
            LastName = lastName,
            IdirName = idirName,
            Email = $"{idirName}@example.com",
            IsEnabled = true,
        };

    private static CalendarConflictParticipant CreateParticipant(
        int id,
        int startHour,
        int endHour,
        int endMinute = 0
    ) =>
        new(
            id,
            "assignment",
            "scheduling",
            ResourceId,
            Baseline(startHour),
            Baseline(endHour).AddMinutes(endMinute),
            $"Assignment {id}"
        );

    private static DateTimeOffset Baseline(int hour) => new(2026, 7, 1, hour, 0, 0, TimeSpan.Zero);

    private sealed class MutableProvider : ICalendarConflictParticipantProvider
    {
        public IReadOnlyCollection<CalendarConflictParticipant> Participants { get; set; } = [];

        public Task<IReadOnlyCollection<CalendarConflictParticipant>> GetParticipantsAsync(
            CalendarConflictQuery query,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(Participants);
    }
}
