using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Unified.Calendar.Conflicts;
using Unified.Calendar.Models;
using Unified.Common.Calendar.Conflicts;
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
                FirstSourceModule = "scheduling",
                FirstEventId = "2",
                SecondSourceModule = "scheduling",
                SecondEventId = "1",
                ResourceId = ResourceId,
                Note = "Manager approved",
            },
            ActorId,
            TestContext.Current.CancellationToken
        );

        var persistedOverride = await _db.CalendarConflictOverrides.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("scheduling", persistedOverride.FirstSourceModule);
        Assert.Equal("1", persistedOverride.FirstEventId);
        Assert.Equal("scheduling", persistedOverride.SecondSourceModule);
        Assert.Equal("2", persistedOverride.SecondEventId);
        Assert.Equal(ActorId, persistedOverride.CreatedById);
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
                FirstSourceModule = "scheduling",
                FirstEventId = "1",
                SecondSourceModule = "scheduling",
                SecondEventId = "2",
                ResourceId = ResourceId,
                Note = "Temporary",
            },
            ActorId,
            TestContext.Current.CancellationToken
        );

        _provider.Participants = [CreateParticipant(1, 8, 10), CreateParticipant(2, 10, 11)];

        await _service.InvalidateResolvedOverridesAsync(
            [new CalendarConflictEventIdentity("scheduling", "1")],
            cancellationToken: TestContext.Current.CancellationToken
        );

        var persisted = await _db.CalendarConflictOverrides.SingleAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(persisted.InvalidatedOn);
    }

    [Fact]
    public async Task InvalidateResolvedOverridesAsync_WhenPairStillConflicts_PreservesOverride()
    {
        await _service.CreateOverrideAsync(
            new CalendarConflictAcknowledgement
            {
                FirstSourceModule = "scheduling",
                FirstEventId = "1",
                SecondSourceModule = "scheduling",
                SecondEventId = "2",
                ResourceId = ResourceId,
                Note = "Original state",
            },
            ActorId,
            TestContext.Current.CancellationToken
        );
        _provider.Participants = [CreateParticipant(1, 8, 10), CreateParticipant(2, 9, 10, 30)];

        await _service.InvalidateResolvedOverridesAsync(
            [new CalendarConflictEventIdentity("scheduling", "1")],
            cancellationToken: TestContext.Current.CancellationToken
        );

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
                FirstSourceModule = "scheduling",
                FirstEventId = "1",
                SecondSourceModule = "scheduling",
                SecondEventId = "2",
                ResourceId = ResourceId,
                Note = "Initial approval",
            },
            creatorId,
            TestContext.Current.CancellationToken
        );
        await _service.CreateOverrideAsync(
            new CalendarConflictAcknowledgement
            {
                FirstSourceModule = "scheduling",
                FirstEventId = "1",
                SecondSourceModule = "scheduling",
                SecondEventId = "2",
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
                new CalendarConflictParticipant("2", "training", ResourceId, Baseline(9), Baseline(11), "Training 2"),
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
    public async Task CreateOverrideAsync_WithExternalParticipant_PersistsSourceQualifiedIdentity()
    {
        var training = new MutableProvider
        {
            Participants =
            [
                new CalendarConflictParticipant(
                    "course-42",
                    "training",
                    ResourceId,
                    Baseline(9),
                    Baseline(11),
                    "Course 42"
                ),
            ],
        };
        var service = new CalendarConflictService([_provider, training], _db);

        await service.CreateOverrideAsync(
            new CalendarConflictAcknowledgement
            {
                FirstSourceModule = "training",
                FirstEventId = "course-42",
                SecondSourceModule = "scheduling",
                SecondEventId = "1",
                ResourceId = ResourceId,
                Note = "Approved",
            },
            ActorId,
            TestContext.Current.CancellationToken
        );

        var persisted = await _db.CalendarConflictOverrides.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("scheduling", persisted.FirstSourceModule);
        Assert.Equal("1", persisted.FirstEventId);
        Assert.Equal("training", persisted.SecondSourceModule);
        Assert.Equal("course-42", persisted.SecondEventId);
    }

    [Fact]
    public async Task CreateOverrideAsync_WhenParticipantCannotBeResolved_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.CreateOverrideAsync(
                new CalendarConflictAcknowledgement
                {
                    FirstSourceModule = "scheduling",
                    FirstEventId = "1",
                    SecondSourceModule = "training",
                    SecondEventId = "missing",
                    ResourceId = ResourceId,
                    Note = "Approved",
                },
                ActorId,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task CreateOverrideAsync_WhenMultipleProvidersResolveParticipant_ThrowsInvalidOperationException()
    {
        var duplicateProvider = new MutableProvider { Participants = _provider.Participants };
        var service = new CalendarConflictService([_provider, duplicateProvider], _db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateOverrideAsync(
                CreateAcknowledgement(1, 2, "Approved"),
                ActorId,
                TestContext.Current.CancellationToken
            )
        );
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
        Assert.Equal("1", persisted.FirstEventId);
        Assert.Equal("2", persisted.SecondEventId);
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
                FirstSourceModule = "scheduling",
                FirstEventId = "1",
                SecondSourceModule = "scheduling",
                SecondEventId = "2",
                ResourceId = ResourceId,
                Note = "Already approved",
            },
            ActorId,
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
            FirstSourceModule = "scheduling",
            FirstEventId = firstEventId.ToString(CultureInfo.InvariantCulture),
            SecondSourceModule = "scheduling",
            SecondEventId = secondEventId.ToString(CultureInfo.InvariantCulture),
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
            id.ToString(CultureInfo.InvariantCulture),
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

        Task<CalendarConflictParticipant?> ICalendarConflictParticipantProvider.GetParticipantAsync(
            CalendarConflictEventIdentity identity,
            Guid resourceId,
            CancellationToken _cancellationToken = default
        )
        {
            _cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(
                Participants.SingleOrDefault(participant =>
                    participant.Identity == identity && participant.ResourceId == resourceId
                )
            );
        }

        Task<IReadOnlyCollection<CalendarConflictParticipant>> ICalendarConflictParticipantProvider.GetParticipantsAsync(
            CalendarConflictQuery _query,
            CancellationToken _cancellationToken = default
        )
        {
            _ = _query;
            _cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Participants);
        }
    }

    private sealed class BlockingProvider : ICalendarConflictParticipantProvider
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<CalendarConflictParticipant?> ICalendarConflictParticipantProvider.GetParticipantAsync(
            CalendarConflictEventIdentity _identity,
            Guid _resourceId,
            CancellationToken _cancellationToken = default
        )
        {
            _ = Started;
            _ = _identity;
            _ = _resourceId;
            _cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<CalendarConflictParticipant?>(null);
        }

        async Task<IReadOnlyCollection<CalendarConflictParticipant>> ICalendarConflictParticipantProvider.GetParticipantsAsync(
            CalendarConflictQuery _query,
            CancellationToken cancellationToken = default
        )
        {
            _ = _query;
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return [];
        }
    }

    private sealed class TrackingProvider : ICalendarConflictParticipantProvider
    {
        public bool WasCalled { get; private set; }

        Task<CalendarConflictParticipant?> ICalendarConflictParticipantProvider.GetParticipantAsync(
            CalendarConflictEventIdentity _identity,
            Guid _resourceId,
            CancellationToken _cancellationToken = default
        )
        {
            _ = WasCalled;
            _ = _identity;
            _ = _resourceId;
            _cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<CalendarConflictParticipant?>(null);
        }

        Task<IReadOnlyCollection<CalendarConflictParticipant>> ICalendarConflictParticipantProvider.GetParticipantsAsync(
            CalendarConflictQuery _query,
            CancellationToken _cancellationToken = default
        )
        {
            _ = _query;
            _cancellationToken.ThrowIfCancellationRequested();
            WasCalled = true;
            return Task.FromResult<IReadOnlyCollection<CalendarConflictParticipant>>([]);
        }
    }
}
