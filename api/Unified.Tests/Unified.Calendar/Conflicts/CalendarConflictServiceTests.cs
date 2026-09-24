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
    private static readonly Guid ActorId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
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
        _db.Users.Add(CreateUser(ActorId, "Calendar", "Approver", "calendar-approver"));
        _db.Events.AddRange(CreateEvent(1, 8, 10), CreateEvent(2, 9, 11));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        _provider = new MutableProvider { Participants = [CreateParticipant(1, 8, 10), CreateParticipant(2, 9, 11)] };
        _service = new CalendarConflictService([_provider], _db);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task CreateOverrideAsync_NormalizesPairAndMarksRetrievedConflictOverridden()
    {
        await _service.CreateOverrideAsync(
            new CalendarConflictAcknowledgement
            {
                FirstEventId = 2,
                SecondEventId = 1,
                ResourceId = ResourceId,
                Note = "Manager approved",
            },
            null,
            TestContext.Current.CancellationToken
        );

        var persistedOverride = await _db.CalendarConflictOverrides.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, persistedOverride.FirstEventId);
        Assert.Equal(2, persistedOverride.SecondEventId);
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
            new CalendarConflictAcknowledgement
            {
                FirstEventId = 1,
                SecondEventId = 2,
                ResourceId = ResourceId,
                Note = "Temporary",
            },
            null,
            TestContext.Current.CancellationToken
        );

        _provider.Participants = [CreateParticipant(1, 8, 10), CreateParticipant(2, 10, 11)];

        await _service.InvalidateResolvedOverridesAsync([1], cancellationToken: TestContext.Current.CancellationToken);

        var persisted = await _db.CalendarConflictOverrides.SingleAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(persisted.InvalidatedOn);
    }

    [Fact]
    public async Task InvalidateResolvedOverridesAsync_WhenPairStillConflicts_PreservesOverride()
    {
        await _service.CreateOverrideAsync(
            new CalendarConflictAcknowledgement
            {
                FirstEventId = 1,
                SecondEventId = 2,
                ResourceId = ResourceId,
                Note = "Original state",
            },
            null,
            TestContext.Current.CancellationToken
        );
        _provider.Participants = [CreateParticipant(1, 8, 10), CreateParticipant(2, 9, 10, 30)];

        await _service.InvalidateResolvedOverridesAsync([1], cancellationToken: TestContext.Current.CancellationToken);

        var conflict = Assert.Single(
            await _service.GetConflictsAsync(
                new CalendarConflictQuery(Baseline(7), Baseline(12)),
                TestContext.Current.CancellationToken
            )
        );

        Assert.True(conflict.IsOverridden);
        Assert.Null(
            await _db
                .CalendarConflictOverrides.Select(overrideEntity => overrideEntity.InvalidatedOn)
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
            new CalendarConflictAcknowledgement
            {
                FirstEventId = 1,
                SecondEventId = 2,
                ResourceId = ResourceId,
                Note = "Initial approval",
            },
            creatorId,
            TestContext.Current.CancellationToken
        );
        await _service.CreateOverrideAsync(
            new CalendarConflictAcknowledgement
            {
                FirstEventId = 1,
                SecondEventId = 2,
                ResourceId = ResourceId,
                Note = "Updated approval",
            },
            updaterId,
            TestContext.Current.CancellationToken
        );

        var updated = await _db.CalendarConflictOverrides.SingleAsync(TestContext.Current.CancellationToken);
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

    [Fact]
    public async Task GetConflictsAsync_WithMultipleProviders_QueriesProvidersSequentially()
    {
        var firstProvider = new BlockingProvider();
        var secondProvider = new TrackingProvider();
        var service = new CalendarConflictService([firstProvider, secondProvider], _db);

        var getConflictsTask = service.GetConflictsAsync(
            new CalendarConflictQuery(Baseline(7), Baseline(12)),
            TestContext.Current.CancellationToken
        );
        await firstProvider.Started.Task.WaitAsync(TestContext.Current.CancellationToken);

        Assert.False(secondProvider.WasCalled);

        firstProvider.Release.TrySetResult();
        await getConflictsTask;

        Assert.True(secondProvider.WasCalled);
    }

    [Fact]
    public async Task GetConflictsAsync_WithParticipantsFromDifferentProviders_DetectsCrossModuleConflict()
    {
        var scheduling = new MutableProvider { Participants = [CreateParticipant(1, 8, 10)] };
        var training = new MutableProvider
        {
            Participants =
            [
                new CalendarConflictParticipant(2, "training", ResourceId, Baseline(9), Baseline(11), "Training 2"),
            ],
        };
        var service = new CalendarConflictService([scheduling, training], _db);

        var conflict = Assert.Single(
            await service.GetConflictsAsync(
                new CalendarConflictQuery(Baseline(7), Baseline(12)),
                TestContext.Current.CancellationToken
            )
        );

        Assert.Equal("scheduling", conflict.Entry.SourceModule);
        Assert.Equal("training", conflict.Overlaps.SourceModule);
    }

    [Fact]
    public async Task ValidateAndApplyConflictAcknowledgementsAsync_ReversedAndDuplicateAcknowledgements_PersistsOneOverride()
    {
        var actorId = ActorId;
        await using var transaction = await _db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

        await _service.ValidateAndApplyConflictAcknowledgementsAsync(
            [CreateParticipant(1, 8, 10)],
            [CreateAcknowledgement(2, 1, " Approved "), CreateAcknowledgement(1, 2, "Approved")],
            actorId,
            TestContext.Current.CancellationToken
        );

        var persisted = Assert.Single(
            await _db.CalendarConflictOverrides.ToListAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal(1, persisted.FirstEventId);
        Assert.Equal(2, persisted.SecondEventId);
        Assert.Equal("Approved", persisted.Note);
        Assert.Equal(actorId, persisted.CreatedById);
    }

    [Fact]
    public async Task ValidateAndApplyConflictAcknowledgementsAsync_ConflictingDuplicateNotes_RejectsBeforeWriting()
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ValidateAndApplyConflictAcknowledgementsAsync(
                [CreateParticipant(1, 8, 10)],
                [CreateAcknowledgement(1, 2, "First"), CreateAcknowledgement(2, 1, "Second")],
                Guid.NewGuid(),
                TestContext.Current.CancellationToken
            )
        );

        Assert.Empty(await _db.CalendarConflictOverrides.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ValidateAndApplyConflictAcknowledgementsAsync_StaleAcknowledgement_IsIgnored()
    {
        _provider.Participants = [CreateParticipant(1, 8, 9), CreateParticipant(2, 10, 11)];
        await using var transaction = await _db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

        await _service.ValidateAndApplyConflictAcknowledgementsAsync(
            [CreateParticipant(1, 8, 9)],
            [CreateAcknowledgement(1, 2, "No longer relevant")],
            ActorId,
            TestContext.Current.CancellationToken
        );

        Assert.Empty(await _db.CalendarConflictOverrides.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ValidateAndApplyConflictAcknowledgementsAsync_NewUnacknowledgedConflict_RejectsAllWrites()
    {
        _db.Events.Add(CreateEvent(3, 9, 12));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        _provider.Participants =
        [
            CreateParticipant(1, 8, 10),
            CreateParticipant(2, 9, 11),
            CreateParticipant(3, 9, 12),
        ];
        await using var transaction = await _db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<CalendarConflictException>(() =>
            _service.ValidateAndApplyConflictAcknowledgementsAsync(
                [CreateParticipant(1, 8, 10)],
                [CreateAcknowledgement(1, 2, "Expected")],
                ActorId,
                TestContext.Current.CancellationToken
            )
        );

        Assert.NotEmpty(exception.Conflicts);
        Assert.Empty(await _db.CalendarConflictOverrides.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ValidateAndApplyConflictAcknowledgementsAsync_ExistingActiveOverride_RequiresNoAcknowledgement()
    {
        await _service.CreateOverrideAsync(
            new CalendarConflictAcknowledgement
            {
                FirstEventId = 1,
                SecondEventId = 2,
                ResourceId = ResourceId,
                Note = "Already approved",
            },
            null,
            TestContext.Current.CancellationToken
        );

        await _service.ValidateAndApplyConflictAcknowledgementsAsync(
            [CreateParticipant(1, 8, 10)],
            null,
            null,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task ValidateAndApplyConflictAcknowledgementsAsync_RolledBackTransaction_DoesNotPersistOverride()
    {
        await using (var transaction = await _db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            await _service.ValidateAndApplyConflictAcknowledgementsAsync(
                [CreateParticipant(1, 8, 10)],
                [CreateAcknowledgement(1, 2, "Approved")],
                ActorId,
                TestContext.Current.CancellationToken
            );
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }
        _db.ChangeTracker.Clear();

        Assert.Empty(await _db.CalendarConflictOverrides.ToListAsync(TestContext.Current.CancellationToken));
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

    private static CalendarConflictAcknowledgement CreateAcknowledgement(
        int firstEventId,
        int secondEventId,
        string note
    ) =>
        new()
        {
            FirstEventId = firstEventId,
            SecondEventId = secondEventId,
            ResourceId = ResourceId,
            Note = note,
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

    private sealed class BlockingProvider : ICalendarConflictParticipantProvider
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<IReadOnlyCollection<CalendarConflictParticipant>> GetParticipantsAsync(
            CalendarConflictQuery query,
            CancellationToken cancellationToken = default
        )
        {
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return [];
        }
    }

    private sealed class TrackingProvider : ICalendarConflictParticipantProvider
    {
        public bool WasCalled { get; private set; }

        public Task<IReadOnlyCollection<CalendarConflictParticipant>> GetParticipantsAsync(
            CalendarConflictQuery query,
            CancellationToken cancellationToken = default
        )
        {
            WasCalled = true;
            return Task.FromResult<IReadOnlyCollection<CalendarConflictParticipant>>([]);
        }
    }
}
