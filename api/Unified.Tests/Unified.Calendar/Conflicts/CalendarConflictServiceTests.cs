using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Unified.Calendar.Conflicts;
using Unified.Calendar.Holidays;
using Unified.Calendar.Models;
using Unified.Calendar.Options;
using Unified.Calendar.Services;
using Unified.Common.Calendar.Conflicts;
using Unified.Common.Time;
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
        _service = new CalendarConflictService([_provider], _db, CalendarConflictTestRules.Scheduling);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AssignmentSelfConflicts_ProductionConfiguration_ControlsReportingPublicationAndAcknowledgement(
        bool selfConflictsEnabled
    )
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "api-appsettings.json"), optional: false)
            .Build();
        var rules = configuration
            .GetSection(CalendarConflictRulesOptions.SectionName)
            .Get<CalendarConflictRulesOptions>()!;
        Assert.Contains("assignment", rules.Rules!["assignment"]);
        if (!selfConflictsEnabled)
            rules.Rules["assignment"] = rules.Rules["assignment"].Where(type => type != "assignment").ToArray();
        Assert.Equal(
            new[] { "holiday", "leave", "training" },
            rules.Rules["assignment"].Where(type => type != "assignment")
        );
        var service = new CalendarConflictService([_provider], _db, Options.Create(rules));
        var candidates = new[] { CreateParticipant(1, 8, 10) };
        var conflicts = await service.GetConflictsAsync(
            new(Baseline(7), Baseline(12)),
            TestContext.Current.CancellationToken
        );

        if (!selfConflictsEnabled)
        {
            Assert.Empty(conflicts);
            await service.EnsureNoUnresolvedConflictsAsync(candidates, TestContext.Current.CancellationToken);
            return;
        }

        var conflict = Assert.Single(conflicts);
        Assert.True(conflict.ReportOnEntry);
        Assert.True(conflict.ReportOnOverlaps);
        await Assert.ThrowsAsync<CalendarConflictException>(() =>
            service.EnsureNoUnresolvedConflictsAsync(candidates, TestContext.Current.CancellationToken)
        );
        await using var transaction = await _db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await service.ValidateAndApplyConflictAcknowledgementsAsync(
            candidates,
            [CreateAcknowledgement(1, 2, "Approved")],
            ActorId,
            TestContext.Current.CancellationToken
        );
        Assert.True(
            Assert
                .Single(await service.GetConflictsForCandidatesAsync(candidates, TestContext.Current.CancellationToken))
                .IsOverridden
        );
        await service.EnsureNoUnresolvedConflictsAsync(candidates, TestContext.Current.CancellationToken);
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
    public async Task GetConflictsAsync_WithLocationScope_ReturnsOnlyConflictsTouchingScopedParticipants()
    {
        var provider = new MutableProvider
        {
            Participants =
            [
                CreateParticipant(1, 8, 10),
                CreateParticipant(2, 9, 11) with
                {
                    IsInQueryScope = false,
                },
                CreateParticipant(3, 12, 14) with
                {
                    IsInQueryScope = false,
                },
                CreateParticipant(4, 13, 15) with
                {
                    IsInQueryScope = false,
                },
            ],
        };
        var service = new CalendarConflictService([provider], _db, CalendarConflictTestRules.Scheduling);

        var conflicts = await service.GetConflictsAsync(
            new CalendarConflictQuery(Baseline(7), Baseline(16), LocationId: 5),
            TestContext.Current.CancellationToken
        );

        var conflict = Assert.Single(conflicts);
        Assert.Equal(new[] { "1", "2" }, new[] { conflict.Entry.EventId, conflict.Overlaps.EventId });
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
    public async Task InvalidateResolvedOverridesAsync_WhenAssignmentTimeZoneChanges_InvalidatesHolidayOverride()
    {
        var assignmentProvider = new MutableProvider
        {
            Participants =
            [
                new CalendarConflictParticipant(
                    "1",
                    "scheduling",
                    ActorId,
                    new DateTimeOffset(2026, 7, 1, 13, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 7, 1, 14, 0, 0, TimeSpan.Zero),
                    "Assignment 1",
                    TimeZoneId: "America/Vancouver",
                    EventTypeCode: "assignment"
                ),
            ],
        };
        var service = new CalendarConflictService(
            [CreateHolidayProvider(), assignmentProvider],
            _db,
            CalendarConflictTestRules.Scheduling
        );
        await service.CreateOverrideAsync(
            new CalendarConflictAcknowledgement
            {
                FirstSourceModule = "calendar",
                FirstEventId = "stat-holiday:CanadaDay:2026-07-01:America/Vancouver",
                SecondSourceModule = "scheduling",
                SecondEventId = "1",
                ResourceId = ActorId,
                Note = "Approved for Vancouver",
            },
            ActorId,
            TestContext.Current.CancellationToken
        );
        assignmentProvider.Participants =
        [
            assignmentProvider.Participants.Single() with
            {
                TimeZoneId = "America/Toronto",
            },
        ];

        await service.InvalidateResolvedOverridesAsync(
            [new CalendarConflictEventIdentity("scheduling", "1")],
            cancellationToken: TestContext.Current.CancellationToken
        );

        var persisted = await _db.CalendarConflictOverrides.SingleAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(persisted.InvalidatedOn);

        assignmentProvider.Participants =
        [
            assignmentProvider.Participants.Single() with
            {
                TimeZoneId = "America/Vancouver",
            },
        ];
        var conflictAfterReturningToVancouver = Assert.Single(
            await service.GetConflictsAsync(
                new CalendarConflictQuery(
                    new DateTimeOffset(2026, 7, 1, 7, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 7, 2, 7, 0, 0, TimeSpan.Zero),
                    ResourceIds: [ActorId],
                    TimeZoneIds: ["America/Vancouver"],
                    ResourceTimeZones: [new(ActorId, "America/Vancouver")]
                ),
                TestContext.Current.CancellationToken
            )
        );
        Assert.False(conflictAfterReturningToVancouver.IsOverridden);
    }

    [Theory]
    [InlineData(" America/Vancouver ", "America/Vancouver")]
    [InlineData("Pacific Standard Time", "America/Los_Angeles")]
    public async Task GetConflictsAsync_WhenOperationalTimeZoneUsesSupportedEquivalentId_NormalizesHolidayContext(
        string assignmentTimeZoneId,
        string expectedTimeZoneId
    )
    {
        var assignmentProvider = new MutableProvider
        {
            Participants =
            [
                new CalendarConflictParticipant(
                    "normalized-zone-assignment",
                    "scheduling",
                    ActorId,
                    new DateTimeOffset(2026, 7, 1, 13, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 7, 1, 14, 0, 0, TimeSpan.Zero),
                    "Assignment",
                    TimeZoneId: assignmentTimeZoneId,
                    EventTypeCode: "assignment"
                ),
            ],
        };
        var service = new CalendarConflictService(
            [CreateHolidayProvider(), assignmentProvider],
            _db,
            CalendarConflictTestRules.Scheduling
        );

        var conflict = Assert.Single(
            await service.GetConflictsAsync(
                new CalendarConflictQuery(
                    new DateTimeOffset(2026, 7, 1, 7, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 7, 2, 7, 0, 0, TimeSpan.Zero),
                    ResourceIds: [ActorId]
                ),
                TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(expectedTimeZoneId, conflict.Entry.TimeZoneId);
        Assert.Equal(expectedTimeZoneId, conflict.Overlaps.TimeZoneId);
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
        var service = new CalendarConflictService(
            [firstProvider, secondProvider],
            _db,
            CalendarConflictTestRules.Scheduling
        );

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
                new CalendarConflictParticipant(
                    "2",
                    "training",
                    ResourceId,
                    Baseline(9),
                    Baseline(11),
                    "Training 2",
                    EventTypeCode: "training"
                ),
            ],
        };
        var service = new CalendarConflictService([scheduling, training], _db, CalendarConflictTestRules.Scheduling);

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
    public async Task GetConflictsForCandidatesAsync_WithNoCandidates_DoesNotQueryProviders()
    {
        var provider = new TrackingProvider();
        var service = new CalendarConflictService([provider], _db, CalendarConflictTestRules.Scheduling);

        var conflicts = await service.GetConflictsForCandidatesAsync([], TestContext.Current.CancellationToken);

        Assert.Empty(conflicts);
        Assert.False(provider.WasCalled);
    }

    [Fact]
    public async Task GetConflictsForCandidatesAsync_WithParticipantTimeZones_ForwardsResourceContexts()
    {
        var provider = new TrackingProvider();
        var service = new CalendarConflictService([provider], _db, CalendarConflictTestRules.Scheduling);
        var candidates = new[]
        {
            CreateParticipant(1, 8, 10) with
            {
                TimeZoneId = "America/Toronto",
            },
            CreateParticipant(2, 9, 11) with
            {
                TimeZoneId = "America/Toronto",
            },
        };

        await service.GetConflictsForCandidatesAsync(candidates, TestContext.Current.CancellationToken);

        Assert.NotNull(provider.LastQuery);
        Assert.Null(provider.LastQuery.TimeZoneIds);
        Assert.Equal(
            new[] { "America/Toronto" },
            provider.LastQuery.ResourceTimeZones?.Select(item => item.TimeZoneId)
        );
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
                    "Course 42",
                    EventTypeCode: "training"
                ),
            ],
        };
        var service = new CalendarConflictService([_provider, training], _db, CalendarConflictTestRules.Scheduling);

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
        var service = new CalendarConflictService(
            [_provider, duplicateProvider],
            _db,
            CalendarConflictTestRules.Scheduling
        );

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

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task GetConflictsAsync_DirectionalRules_PreserveOnePairAndStableSides(bool forward, bool reverse)
    {
        var first = CreateParticipant(1, 8, 10);
        var second = CreateParticipant(2, 9, 11) with { EventTypeCode = "training", SourceModule = "training" };
        _provider.Participants = [second, first];
        var service = new CalendarConflictService([_provider], _db, DirectionalRules(forward, reverse));

        var conflicts = await service.GetConflictsAsync(
            new(Baseline(7), Baseline(12)),
            TestContext.Current.CancellationToken
        );

        if (!forward && !reverse)
        {
            Assert.Empty(conflicts);
            return;
        }
        var conflict = Assert.Single(conflicts);
        Assert.Equal(first, conflict.Entry);
        Assert.Equal(second, conflict.Overlaps);
        Assert.Equal(forward, conflict.ReportOnEntry);
        Assert.Equal(reverse, conflict.ReportOnOverlaps);
        Assert.Equal(Assert.Single(CalendarConflictDetector.Detect([first, second])).Id, conflict.Id);
        var response = CalendarConflictResponse.FromConflict(conflict);
        Assert.Equal(forward, response.ReportOnEntry);
        Assert.Equal(reverse, response.ReportOnOverlaps);
    }

    [Theory]
    [InlineData(true, false, true, true)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, true, false)]
    [InlineData(false, true, false, true)]
    [InlineData(true, true, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(false, false, true, false)]
    public async Task GetConflictsForCandidatesAsync_BlocksOnlyReportableCandidate(
        bool forward,
        bool reverse,
        bool candidateIsEntry,
        bool expected
    )
    {
        var first = CreateParticipant(1, 8, 10);
        var second = CreateParticipant(2, 9, 11) with { EventTypeCode = "training", SourceModule = "training" };
        _provider.Participants = [first, second];
        var service = new CalendarConflictService([_provider], _db, DirectionalRules(forward, reverse));
        CalendarConflictParticipant[] candidates = [candidateIsEntry ? first : second];

        var conflicts = await service.GetConflictsForCandidatesAsync(candidates, TestContext.Current.CancellationToken);

        Assert.Equal(expected ? 1 : 0, conflicts.Count);
        if (expected)
            await Assert.ThrowsAsync<CalendarConflictException>(() =>
                service.EnsureNoUnresolvedConflictsAsync(candidates, TestContext.Current.CancellationToken)
            );
        else
            await service.ValidateAndApplyConflictAcknowledgementsAsync(
                candidates,
                null,
                null,
                TestContext.Current.CancellationToken
            );
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    public async Task GetConflictsAsync_LocationScope_RequiresReportableScopedSide(
        bool forward,
        bool reverse,
        bool expected
    )
    {
        _provider.Participants =
        [
            CreateParticipant(1, 8, 10),
            CreateParticipant(2, 9, 11) with
            {
                EventTypeCode = "training",
                IsInQueryScope = false,
            },
        ];
        var service = new CalendarConflictService([_provider], _db, DirectionalRules(forward, reverse));

        var conflicts = await service.GetConflictsAsync(
            new(Baseline(7), Baseline(12), LocationId: 5),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(expected ? 1 : 0, conflicts.Count);
    }

    [Theory]
    [InlineData("shift", false)]
    [InlineData("assignment", true)]
    public async Task GetConflictsAsync_TypesSharingSourceModule_UseIndependentRules(string type, bool expected)
    {
        _provider.Participants =
        [
            CreateParticipant(1, 8, 10) with
            {
                EventTypeCode = type,
            },
            CreateParticipant(2, 9, 11) with
            {
                EventTypeCode = "training",
            },
        ];
        var service = new CalendarConflictService([_provider], _db, DirectionalRules(true, false));

        var conflicts = await service.GetConflictsAsync(
            new(Baseline(7), Baseline(12)),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(expected ? 1 : 0, conflicts.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Overrides_WhenRulesDisabled_PreservePhysicalInvalidationAndReuse(bool overlapEnds)
    {
        _provider.Participants =
        [
            CreateParticipant(1, 8, 10),
            CreateParticipant(2, 9, 11) with
            {
                EventTypeCode = "training",
            },
        ];
        var enabled = new CalendarConflictService([_provider], _db, DirectionalRules(true, false));
        var query = new CalendarConflictQuery(Baseline(7), Baseline(12));
        var original = Assert.Single(await enabled.GetConflictsAsync(query, TestContext.Current.CancellationToken));
        await enabled.CreateOverrideAsync(
            CreateAcknowledgement(2, 1, "Approved"),
            ActorId,
            TestContext.Current.CancellationToken
        );
        var disabled = new CalendarConflictService([_provider], _db, DirectionalRules(false, false));

        Assert.Empty(await disabled.GetConflictsAsync(query, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            disabled.CreateOverrideAsync(
                CreateAcknowledgement(1, 2, "Disabled"),
                ActorId,
                TestContext.Current.CancellationToken
            )
        );
        if (overlapEnds)
            _provider.Participants =
            [
                CreateParticipant(1, 8, 10),
                CreateParticipant(2, 10, 11) with
                {
                    EventTypeCode = "training",
                },
            ];
        await disabled.InvalidateResolvedOverridesAsync(
            [new("scheduling", "1")],
            cancellationToken: TestContext.Current.CancellationToken
        );

        var stored = await _db.CalendarConflictOverrides.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(overlapEnds, stored.InvalidatedOn.HasValue);
        var reversed = new CalendarConflictService([_provider], _db, DirectionalRules(false, true));
        var conflicts = await reversed.GetConflictsAsync(query, TestContext.Current.CancellationToken);
        if (overlapEnds)
            Assert.Empty(conflicts);
        else
        {
            var conflict = Assert.Single(conflicts);
            Assert.Equal(original.Id, conflict.Id);
            Assert.Equal(CalendarConflictKey.Create(original), CalendarConflictKey.Create(conflict));
            Assert.True(conflict.IsOverridden);
            Assert.False(conflict.ReportOnEntry);
            Assert.True(conflict.ReportOnOverlaps);
            await reversed.ValidateAndApplyConflictAcknowledgementsAsync(
                [conflict.Overlaps],
                null,
                null,
                TestContext.Current.CancellationToken
            );
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetConflictsForCandidatesAsync_TrainingAgainstHoliday_OnlyTrainingIsReportable(
        bool candidateIsTraining
    )
    {
        var training = CreateParticipant(1, 8, 10) with { EventTypeCode = "training", SourceModule = "training" };
        var holiday = CreateParticipant(2, 7, 12) with { EventTypeCode = "holiday", SourceModule = "calendar" };
        _provider.Participants = [holiday, training];
        var service = new CalendarConflictService(
            [_provider],
            _db,
            CalendarConflictTestRules.Create(("training", ["holiday"]), ("holiday", []))
        );

        var conflicts = await service.GetConflictsForCandidatesAsync(
            [candidateIsTraining ? training : holiday],
            TestContext.Current.CancellationToken
        );

        Assert.Equal(candidateIsTraining ? 1 : 0, conflicts.Count);
        if (candidateIsTraining)
        {
            var conflict = Assert.Single(conflicts);
            Assert.Equal(holiday, conflict.Entry);
            Assert.False(conflict.ReportOnEntry);
            Assert.True(conflict.ReportOnOverlaps);
        }
    }

    [Theory]
    [InlineData("leave", "holiday")]
    [InlineData("leave", "leave")]
    [InlineData("shift", "holiday")]
    [InlineData("shift", "assignment")]
    public async Task GetConflictsAsync_PreviouslyRestrictedPairs_AreControlledByConfiguration(
        string source,
        string target
    )
    {
        static string ModuleFor(string type) =>
            type switch
            {
                "leave" => "leave",
                "holiday" => "calendar",
                _ => "scheduling",
            };
        _provider.Participants =
        [
            CreateParticipant(1, 8, 10) with
            {
                EventTypeCode = source,
                SourceModule = ModuleFor(source),
            },
            CreateParticipant(2, 9, 11) with
            {
                EventTypeCode = target,
                SourceModule = ModuleFor(target),
            },
        ];
        var service = new CalendarConflictService(
            [_provider],
            _db,
            CalendarConflictTestRules.Create((source, [target]))
        );

        var conflict = Assert.Single(
            await service.GetConflictsAsync(new(Baseline(7), Baseline(12)), TestContext.Current.CancellationToken)
        );

        Assert.True(conflict.ReportOnEntry);
        Assert.Equal(source == target, conflict.ReportOnOverlaps);
    }

    private static IOptions<CalendarConflictRulesOptions> DirectionalRules(bool forward, bool reverse) =>
        CalendarConflictTestRules.Create(
            ("assignment", forward ? ["training"] : []),
            ("training", reverse ? ["assignment"] : [])
        );

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
            $"Assignment {id}",
            EventTypeCode: "assignment"
        );

    private static DateTimeOffset Baseline(int hour) => new(2026, 7, 1, hour, 0, 0, TimeSpan.Zero);

    private HolidayConflictParticipantProvider CreateHolidayProvider()
    {
        var timeZoneService = new TimeZoneService();
        var timeZoneResolver = new CalendarTimeZoneResolver(
            Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "America/Vancouver" }),
            timeZoneService
        );
        return new HolidayConflictParticipantProvider(
            _db,
            new StatutoryHolidayCalendarDataProvider(new BcStatutoryHolidayCalculator(), timeZoneService),
            timeZoneResolver,
            timeZoneService
        );
    }

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

        Task<
            IReadOnlyCollection<CalendarConflictParticipant>
        > ICalendarConflictParticipantProvider.GetParticipantsAsync(
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

        async Task<
            IReadOnlyCollection<CalendarConflictParticipant>
        > ICalendarConflictParticipantProvider.GetParticipantsAsync(
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
        public CalendarConflictQuery? LastQuery { get; private set; }

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

        Task<
            IReadOnlyCollection<CalendarConflictParticipant>
        > ICalendarConflictParticipantProvider.GetParticipantsAsync(
            CalendarConflictQuery query,
            CancellationToken _cancellationToken = default
        )
        {
            _cancellationToken.ThrowIfCancellationRequested();
            WasCalled = true;
            LastQuery = query;
            return Task.FromResult<IReadOnlyCollection<CalendarConflictParticipant>>([]);
        }
    }
}
