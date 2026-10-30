using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Unified.Calendar.Conflicts;
using Unified.Calendar.Models;
using Unified.Calendar.Options;
using Unified.Calendar.Services;
using Unified.Common.Calendar.Conflicts;
using Unified.Common.Time;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Training;
using Unified.Db.Models.UserManagement;
using Unified.Tests.TestHelpers;
using Unified.Training.Models;
using Unified.Training.Services;

namespace Unified.Tests.Training.Services;

public sealed class TrainingConflictLifecycleTests : IAsyncLifetime
{
    private static readonly Guid UserId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorId = new("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset AssignmentStart = Day(2026, 10, 22).AddHours(17);
    private static readonly DateTimeOffset AssignmentEnd = AssignmentStart.AddHours(1);
    private UnifiedDbContext _db = null!;
    private CalendarConflictService _conflicts = null!;
    private UserTrainingService _service = null!;
    private AssignmentProvider _assignmentProvider = null!;
    private TrainingConflictParticipantProvider _trainingProvider = null!;

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
                Id = UserId,
                IdirName = "training-user",
                HomeLocationId = 1,
            },
            new User { Id = ActorId, IdirName = "training-actor" }
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
        _trainingProvider = new TrainingConflictParticipantProvider(
            _db,
            new CalendarTimeZoneResolver(
                Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "UTC" }),
                timeZoneService
            ),
            timeZoneService
        );
        _assignmentProvider = new AssignmentProvider();
        _conflicts = new CalendarConflictService(
            [_trainingProvider, _assignmentProvider],
            _db,
            CalendarConflictTestRules.Scheduling
        );
        _service = new UserTrainingService(_db, _conflicts);
    }

    public async ValueTask DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task CreateAsync_WhenAssignmentOverlaps_AllowsTraining()
    {
        var created = await _service.CreateAsync(Request(Day(2026, 10, 22)), TestContext.Current.CancellationToken);

        Assert.True(created.Id > 0);
        Assert.Single(await ConflictsAsync());
    }

    [Fact]
    public async Task UpdateAsync_WhenConflictRemains_PreservesOverride()
    {
        var record = await CreateOverriddenTrainingAsync();

        await _service.UpdateAsync(
            record.Id,
            Request(Day(2026, 10, 21), Day(2026, 10, 22)),
            TestContext.Current.CancellationToken
        );

        Assert.Null(
            (await _db.CalendarConflictOverrides.SingleAsync(TestContext.Current.CancellationToken)).InvalidatedOn
        );
        Assert.True(Assert.Single(await ConflictsAsync()).IsOverridden);
    }

    [Fact]
    public async Task UpdateAsync_WhenConflictEnds_InvalidatesOverrideAndDoesNotReactivateIt()
    {
        var record = await CreateOverriddenTrainingAsync();

        await _service.UpdateAsync(record.Id, Request(Day(2026, 10, 23)), TestContext.Current.CancellationToken);
        Assert.NotNull(
            (await _db.CalendarConflictOverrides.SingleAsync(TestContext.Current.CancellationToken)).InvalidatedOn
        );
        Assert.Empty(await ConflictsAsync());

        await _service.UpdateAsync(record.Id, Request(Day(2026, 10, 22)), TestContext.Current.CancellationToken);
        Assert.False(Assert.Single(await ConflictsAsync()).IsOverridden);
    }

    [Fact]
    public async Task DeleteAsync_WhenOverridden_InvalidatesOverride()
    {
        var record = await CreateOverriddenTrainingAsync();

        Assert.True(await _service.DeleteAsync(record.Id, TestContext.Current.CancellationToken));

        Assert.Empty(await ConflictsAsync());
        Assert.NotNull(
            (await _db.CalendarConflictOverrides.SingleAsync(TestContext.Current.CancellationToken)).InvalidatedOn
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Mutation_WithoutCalendarService_InvalidatesOnlyMatchingActiveOverrides(bool delete)
    {
        var record = await CreateOverriddenTrainingAsync();
        var identity = TrainingConflictParticipantProvider.IdentityFor(record.Id);
        var createdOn = Day(2026, 10, 1);
        _db.CalendarConflictOverrides.AddRange(
            new CalendarConflictOverride
            {
                FirstSourceModule = "training",
                FirstEventId = identity.EventId,
                SecondSourceModule = "scheduling",
                SecondEventId = "97",
                ResourceId = UserId,
                Note = "Training first",
                CreatedById = ActorId,
                CreatedOn = createdOn,
            },
            new CalendarConflictOverride
            {
                FirstSourceModule = "leave",
                FirstEventId = identity.EventId,
                SecondSourceModule = "scheduling",
                SecondEventId = "99",
                ResourceId = UserId,
                Note = "Different module",
                CreatedById = ActorId,
                CreatedOn = createdOn,
            },
            new CalendarConflictOverride
            {
                FirstSourceModule = "training",
                FirstEventId = "training.user-training.999",
                SecondSourceModule = "scheduling",
                SecondEventId = "98",
                ResourceId = UserId,
                Note = "Different Training",
                CreatedById = ActorId,
                CreatedOn = createdOn,
            }
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var standaloneService = new UserTrainingService(_db);
        if (delete)
            Assert.True(await standaloneService.DeleteAsync(record.Id, TestContext.Current.CancellationToken));
        else
            await standaloneService.UpdateAsync(
                record.Id,
                Request(Day(2026, 10, 23)),
                TestContext.Current.CancellationToken
            );

        var matching = await _db
            .CalendarConflictOverrides.Where(item =>
                (item.FirstSourceModule == "training" && item.FirstEventId == identity.EventId)
                || (item.SecondSourceModule == "training" && item.SecondEventId == identity.EventId)
            )
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, matching.Count);
        Assert.All(
            matching,
            item =>
            {
                Assert.NotNull(item.InvalidatedOn);
                Assert.Equal(item.InvalidatedOn, item.UpdatedOn);
                Assert.Equal(ActorId, item.CreatedById);
            }
        );
        var matchingIds = matching.Select(item => item.Id).ToList();
        var unrelated = await _db
            .CalendarConflictOverrides.Where(item => !matchingIds.Contains(item.Id))
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, unrelated.Count);
        Assert.All(
            unrelated,
            item =>
            {
                Assert.Null(item.InvalidatedOn);
                Assert.Null(item.UpdatedOn);
                Assert.Equal(ActorId, item.CreatedById);
                Assert.Equal(createdOn, item.CreatedOn);
            }
        );

        if (!delete)
        {
            await _service.UpdateAsync(record.Id, Request(Day(2026, 10, 22)), TestContext.Current.CancellationToken);
            Assert.False(Assert.Single(await ConflictsAsync()).IsOverridden);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Mutation_WithoutCalendarService_WhenInvalidationFails_RollsBackTraining(bool delete)
    {
        var record = await CreateOverriddenTrainingAsync();
        await _db.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER FailOverrideUpdate BEFORE UPDATE ON CalendarConflictOverrides BEGIN SELECT RAISE(FAIL, 'blocked'); END;",
            TestContext.Current.CancellationToken
        );
        var standaloneService = new UserTrainingService(_db);

        if (delete)
            await Assert.ThrowsAsync<DbUpdateException>(() =>
                standaloneService.DeleteAsync(record.Id, TestContext.Current.CancellationToken)
            );
        else
            await Assert.ThrowsAsync<DbUpdateException>(() =>
                standaloneService.UpdateAsync(
                    record.Id,
                    Request(Day(2026, 10, 23)),
                    TestContext.Current.CancellationToken
                )
            );

        _db.ChangeTracker.Clear();
        Assert.Equal(
            Day(2026, 10, 22),
            (await _db.UserTrainings.SingleAsync(TestContext.Current.CancellationToken)).AwardedOn
        );
        Assert.Null(
            (await _db.CalendarConflictOverrides.SingleAsync(TestContext.Current.CancellationToken)).InvalidatedOn
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Mutation_WhenOverrideRecheckFails_RollsBackTrainingAndOverride(bool delete)
    {
        var record = await CreateOverriddenTrainingAsync();
        _assignmentProvider.ThrowOnLookup = true;

        if (delete)
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.DeleteAsync(record.Id, TestContext.Current.CancellationToken)
            );
        else
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.UpdateAsync(record.Id, Request(Day(2026, 10, 23)), TestContext.Current.CancellationToken)
            );

        _db.ChangeTracker.Clear();
        var persisted = await _db.UserTrainings.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(Day(2026, 10, 22), persisted.AwardedOn);
        Assert.Null(
            (await _db.CalendarConflictOverrides.SingleAsync(TestContext.Current.CancellationToken)).InvalidatedOn
        );
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task TrainingConflicts_ConfiguredDirections_ReportAndBlockOnlyConfiguredSide(
        bool reportOnAssignment,
        bool reportOnTraining
    )
    {
        var record = await _service.CreateAsync(Request(Day(2026, 10, 22)), TestContext.Current.CancellationToken);
        var query = new CalendarConflictQuery(Day(2026, 10, 22), Day(2026, 10, 23), [UserId]);
        var assignment = Assert.Single(
            await _assignmentProvider.GetParticipantsAsync(query, TestContext.Current.CancellationToken)
        );
        var training = await _trainingProvider.GetParticipantAsync(
            TrainingConflictParticipantProvider.IdentityFor(record.Id),
            UserId,
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(training);
        var service = new CalendarConflictService(
            [_trainingProvider, _assignmentProvider],
            _db,
            CalendarConflictTestRules.Create(
                ("assignment", reportOnAssignment ? ["training"] : []),
                ("training", reportOnTraining ? ["assignment"] : [])
            )
        );

        var conflicts = await service.GetConflictsAsync(query, TestContext.Current.CancellationToken);
        Assert.Equal(reportOnAssignment || reportOnTraining ? 1 : 0, conflicts.Count);
        if (conflicts.Count > 0)
        {
            var conflict = Assert.Single(conflicts);
            Assert.Equal(training, conflict.Entry);
            Assert.Equal(reportOnTraining, conflict.ReportOnEntry);
            Assert.Equal(reportOnAssignment, conflict.ReportOnOverlaps);
        }
        Assert.Equal(
            reportOnAssignment ? 1 : 0,
            (await service.GetConflictsForCandidatesAsync([assignment], TestContext.Current.CancellationToken)).Count
        );
        Assert.Equal(
            reportOnTraining ? 1 : 0,
            (await service.GetConflictsForCandidatesAsync([training], TestContext.Current.CancellationToken)).Count
        );
    }

    private async Task<UserTrainingResponse> CreateOverriddenTrainingAsync()
    {
        var record = await _service.CreateAsync(Request(Day(2026, 10, 22)), TestContext.Current.CancellationToken);
        await _conflicts.CreateOverrideAsync(
            new CalendarConflictAcknowledgement
            {
                FirstSourceModule = "training",
                FirstEventId = TrainingConflictParticipantProvider.IdentityFor(record.Id).EventId,
                SecondSourceModule = "scheduling",
                SecondEventId = "42",
                ResourceId = UserId,
                Note = "Approved",
            },
            ActorId,
            TestContext.Current.CancellationToken
        );
        return record;
    }

    private Task<IReadOnlyCollection<CalendarConflict>> ConflictsAsync() =>
        _conflicts.GetConflictsAsync(
            new CalendarConflictQuery(Day(2026, 10, 22), Day(2026, 10, 23), [UserId]),
            TestContext.Current.CancellationToken
        );

    private static UserTrainingRequest Request(DateTimeOffset start, DateTimeOffset? end = null) =>
        new()
        {
            UserId = UserId,
            TrainingId = 1,
            AwardedOn = start,
            EndingOn = end ?? start,
        };

    private static DateTimeOffset Day(int year, int month, int day) => new(year, month, day, 0, 0, 0, TimeSpan.Zero);

    private sealed class AssignmentProvider : ICalendarConflictParticipantProvider
    {
        private static readonly CalendarConflictParticipant Participant = new(
            "42",
            "scheduling",
            UserId,
            AssignmentStart,
            AssignmentEnd,
            "Assignment",
            EventTypeCode: "assignment"
        );

        public bool ThrowOnLookup { get; set; }

        public Task<CalendarConflictParticipant?> GetParticipantAsync(
            CalendarConflictEventIdentity identity,
            Guid resourceId,
            CancellationToken cancellationToken = default
        )
        {
            if (ThrowOnLookup)
                throw new InvalidOperationException("Conflict participant lookup failed.");
            return Task.FromResult<CalendarConflictParticipant?>(
                identity == Participant.Identity && resourceId == Participant.ResourceId ? Participant : null
            );
        }

        public Task<IReadOnlyCollection<CalendarConflictParticipant>> GetParticipantsAsync(
            CalendarConflictQuery query,
            CancellationToken cancellationToken = default
        ) =>
            Task.FromResult<IReadOnlyCollection<CalendarConflictParticipant>>(
                (query.ResourceIds is not { Count: > 0 } || query.ResourceIds.Contains(UserId))
                && Participant.Start < query.EndAtUtc
                && Participant.End > query.StartAtUtc
                    ? [Participant]
                    : []
            );
    }
}
