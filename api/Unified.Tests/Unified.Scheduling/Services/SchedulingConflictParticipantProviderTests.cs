using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Unified.Authorization;
using Unified.Calendar.Conflicts;
using Unified.Calendar.Holidays;
using Unified.Calendar.Models;
using Unified.Calendar.Options;
using Unified.Calendar.Services;
using Unified.Common.Calendar.Conflicts;
using Unified.Common.Time;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling;
using Unified.Scheduling.Services;
using Unified.Tests.TestHelpers;
using Unified.Training.Services;

namespace Unified.Tests.Scheduling.Services;

public sealed class SchedulingConflictParticipantProviderTests : IAsyncLifetime
{
    private static readonly Guid UserId = new("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset AssignmentStart = new(2026, 8, 24, 16, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset AssignmentEnd = new(2026, 8, 24, 18, 45, 0, TimeSpan.Zero);
    private static readonly Guid MultiLocationUserId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid UnrelatedUserId = new("33333333-3333-3333-3333-333333333333");
    private UnifiedDbContext _db = null!;
    private SchedulingConflictParticipantProvider _provider = null!;

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new UnifiedDbContext(options);
        _provider = new SchedulingConflictParticipantProvider(_db);

        var shiftEvent = CreateEvent(10, "Visible shift", SchedulingConstants.ShiftEventTypeCode);
        var assignmentEvent = CreateEvent(20, "Restricted assignment", SchedulingConstants.AssignmentEventTypeCode);
        assignmentEvent.StartAtUtc = AssignmentStart;
        assignmentEvent.EndAtUtc = AssignmentEnd;
        shiftEvent.LocationId = 99;
        assignmentEvent.LocationId = 99;
        var link = new ShiftAssignmentEntry
        {
            Id = 30,
            ShiftEntryId = 40,
            ShiftEntry = new ShiftEntry
            {
                Id = 40,
                EventId = shiftEvent.Id,
                Event = shiftEvent,
                Users = [new ShiftEntryUser { UserId = UserId }],
            },
            AssignmentEntryId = 50,
            AssignmentEntry = new AssignmentEntry
            {
                Id = 50,
                EventId = assignmentEvent.Id,
                Event = assignmentEvent,
                AssignmentDefinitionId = 1,
                CategoryId = 1,
                SubCategoryId = 1,
                Capacity = 1,
            },
        };
        _db.ShiftAssignmentEntryUsers.Add(
            new ShiftAssignmentEntryUser
            {
                Id = 60,
                UserId = UserId,
                ShiftAssignmentEntry = link,
            }
        );
        _db.ShiftEntryUsers.Add(
            new ShiftEntryUser
            {
                Id = 61,
                UserId = UserId,
                ShiftEntry = link.ShiftEntry,
            }
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await _db.DisposeAsync();

    [Theory]
    [InlineData(true, true, false, 1, 0)]
    [InlineData(true, false, false, 0, 1)]
    [InlineData(false, false, true, 0, 1)]
    [InlineData(false, false, false, 0, 0)]
    [InlineData(false, true, false, 0, 0)]
    public async Task ConflictProjection_UsesActualEventPermissionsAndVisibleLinkedShiftAnchors(
        bool assignments,
        bool training,
        bool shifts,
        int detailCount,
        int warningCount
    )
    {
        var trainingProvider = await SeedTrainingAsync();
        var accessor = CalendarConflictTestContext.Accessor(UserId);
        accessor.HttpContext!.User = CalendarConflictTestContext.Viewer(
            UserId,
            assignments ? nameof(Permissions.AssignmentsView) : "",
            training ? nameof(Permissions.UserTrainingsView) : "",
            shifts ? nameof(Permissions.ShiftsView) : ""
        );
        var projection = new CalendarConflictProjection([_provider, trainingProvider], accessor);
        var service = new CalendarConflictService([_provider, trainingProvider], _db, projection);
        var time = new TimeZoneService();
        var query = new CalendarQueryService(
            NullLogger<CalendarQueryService>.Instance,
            _db,
            new StatutoryHolidayCalendarDataProvider(new BcStatutoryHolidayCalculator(), time),
            Resolver(time),
            time,
            service,
            projection
        );

        var response = await query.GetCalendarDataAsync(
            new CalendarDataRequest
            {
                StartDate = new DateOnly(2026, 8, 24),
                EndDate = new DateOnly(2026, 8, 24),
                TimeZoneId = "America/Vancouver",
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(detailCount, response.Conflicts.Count);
        Assert.Equal(warningCount, response.RestrictedWarnings.Count);
        Assert.Equal(warningCount > 0, response.HasRestrictedConflicts);
        if (warningCount > 0)
        {
            var warning = Assert.Single(response.RestrictedWarnings);
            Assert.Equal(assignments ? "20" : "10", warning.EventId);
            Assert.Equal(UserId, warning.ResourceId);
        }
        if (detailCount == 0)
        {
            var json = JsonSerializer.Serialize(response);
            Assert.DoesNotContain("SECRET", json);
            Assert.DoesNotContain("training.user-training.77", json);
            Assert.DoesNotContain("OverlapStart", json);
            Assert.DoesNotContain("OverrideNote", json);
            if (!assignments)
                Assert.DoesNotContain("\"20\"", json);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConflictOverride_WithoutUnderlyingVisibility_RejectsDirectAndForgedAcknowledgements(
        bool acknowledgement
    )
    {
        var training = await SeedTrainingAsync();
        var accessor = CalendarConflictTestContext.Accessor(UserId);
        accessor.HttpContext!.User = CalendarConflictTestContext.Viewer(
            UserId,
            nameof(Permissions.CalendarConflictsOverride),
            nameof(Permissions.AssignmentsView)
        );
        var projection = new CalendarConflictProjection([_provider, training], accessor);
        var service = new CalendarConflictService([_provider, training], _db, projection);
        var request = new CalendarConflictAcknowledgement
        {
            FirstSourceModule = "scheduling",
            FirstEventId = "20",
            SecondSourceModule = "training",
            SecondEventId = "training.user-training.77",
            ResourceId = UserId,
            Note = "Forged approval",
        };
        var assignment = (
            await _provider.GetParticipantAsync(new("scheduling", "20"), UserId, TestContext.Current.CancellationToken)
        )!;
        var exception = acknowledgement
            ? await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                service.ValidateAndApplyConflictAcknowledgementsAsync(
                    [assignment],
                    [request],
                    UserId,
                    TestContext.Current.CancellationToken
                )
            )
            : await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                service.CreateOverrideAsync(request, UserId, TestContext.Current.CancellationToken)
            );
        Assert.Equal("The requested conflict is unavailable.", exception.Message);
        var guessed = request with { SecondSourceModule = "scheduling" };
        Assert.Equal(
            exception.Message,
            (
                await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                    service.CreateOverrideAsync(guessed, UserId, TestContext.Current.CancellationToken)
                )
            ).Message
        );
        Assert.Empty(_db.CalendarConflictOverrides);
        await Assert.ThrowsAsync<CalendarConflictException>(() =>
            service.EnsureNoUnresolvedConflictsAsync([assignment], TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task ConflictOverride_AuthorizedPersistedOverride_RemainsEffectiveAfterVisibilityIsLost()
    {
        var training = await SeedTrainingAsync();
        var accessor = CalendarConflictTestContext.Accessor(UserId);
        var projection = new CalendarConflictProjection([_provider, training], accessor);
        var service = new CalendarConflictService([_provider, training], _db, projection);
        await service.CreateOverrideAsync(
            new CalendarConflictAcknowledgement
            {
                FirstSourceModule = "scheduling",
                FirstEventId = "20",
                SecondSourceModule = "training",
                SecondEventId = "training.user-training.77",
                ResourceId = UserId,
                Note = "Private approved note",
            },
            UserId,
            TestContext.Current.CancellationToken
        );
        accessor.HttpContext!.User = CalendarConflictTestContext.Viewer(UserId, nameof(Permissions.AssignmentsView));
        var assignment = (
            await _provider.GetParticipantAsync(new("scheduling", "20"), UserId, TestContext.Current.CancellationToken)
        )!;
        await service.EnsureNoUnresolvedConflictsAsync([assignment], TestContext.Current.CancellationToken);
        var conflicts = await service.GetConflictsForCandidatesAsync(
            [assignment],
            TestContext.Current.CancellationToken
        );
        Assert.True(Assert.Single(conflicts).IsOverridden);
        var visible = await projection.ProjectAsync(conflicts, TestContext.Current.CancellationToken);
        Assert.Empty(visible.Conflicts);
        Assert.Empty(visible.RestrictedWarnings);
        Assert.False(visible.HasRestrictedConflicts);
        Assert.Null(Assert.Single(_db.CalendarConflictOverrides).InvalidatedOn);
    }

    private async Task<TrainingConflictParticipantProvider> SeedTrainingAsync()
    {
        _db.Users.Add(
            new global::Unified.Db.Models.UserManagement.User
            {
                Id = UserId,
                IdirName = "conflict-user",
                IsEnabled = true,
            }
        );
        _db.UserTrainings.Add(
            new global::Unified.Db.Models.Training.UserTraining
            {
                Id = 77,
                UserId = UserId,
                Training = new global::Unified.Db.Models.Training.Training
                {
                    Id = 7,
                    Code = "SECRET",
                    Description = "Restricted course",
                },
                AwardedOn = new DateTimeOffset(2026, 8, 24, 0, 0, 0, TimeSpan.Zero),
                EndingOn = new DateTimeOffset(2026, 8, 24, 0, 0, 0, TimeSpan.Zero),
            }
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var time = new TimeZoneService();
        return new(_db, Resolver(time), time);
    }

    private static CalendarTimeZoneResolver Resolver(ITimeZoneService time) =>
        new(Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "America/Vancouver" }), time);

    [Fact]
    public async Task ConflictProjection_MultipleCandidates_KeepsVisibleDetailsAndEveryRestrictedBlocker()
    {
        var training = await SeedTrainingAsync();
        var secondEvent = CreateEvent(21, "Second visible assignment", SchedulingConstants.AssignmentEventTypeCode);
        secondEvent.StartAtUtc = AssignmentStart;
        secondEvent.EndAtUtc = AssignmentEnd;
        var shift = await _db.ShiftEntries.SingleAsync(TestContext.Current.CancellationToken);
        _db.ShiftAssignmentEntryUsers.Add(
            new ShiftAssignmentEntryUser
            {
                UserId = UserId,
                ShiftAssignmentEntry = new ShiftAssignmentEntry
                {
                    ShiftEntry = shift,
                    ShiftEntryId = shift.Id,
                    AssignmentEntry = new AssignmentEntry
                    {
                        Event = secondEvent,
                        EventId = secondEvent.Id,
                        AssignmentDefinitionId = 1,
                        CategoryId = 1,
                        SubCategoryId = 1,
                        Capacity = 1,
                    },
                },
            }
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var accessor = CalendarConflictTestContext.Accessor(UserId);
        accessor.HttpContext!.User = CalendarConflictTestContext.Viewer(UserId, nameof(Permissions.AssignmentsView));
        var projection = new CalendarConflictProjection([_provider, training], accessor);
        var service = new CalendarConflictService([_provider, training], _db, projection);
        var candidates = await _provider.GetParticipantsAsync(
            new(AssignmentStart, AssignmentEnd),
            TestContext.Current.CancellationToken
        );
        var rejection = await Assert.ThrowsAsync<CalendarConflictException>(() =>
            service.EnsureNoUnresolvedConflictsAsync(candidates, TestContext.Current.CancellationToken)
        );
        Assert.Equal(3, rejection.Conflicts.Count);
        var visible = await projection.ProjectAsync(rejection.Conflicts, TestContext.Current.CancellationToken);
        Assert.Single(visible.Conflicts);
        Assert.Equal(
            new[] { "20", "21" },
            visible.RestrictedWarnings.Select(warning => warning.EventId).Order().ToArray()
        );
        Assert.True(visible.HasRestrictedConflicts);
        Assert.DoesNotContain("SECRET", JsonSerializer.Serialize(visible));
        Assert.DoesNotContain("training.user-training.77", JsonSerializer.Serialize(visible));
    }

    [Fact]
    public async Task GetParticipantsAsync_ReturnsApplicableAssignments()
    {
        var participants = await _provider.GetParticipantsAsync(CreateQuery(), TestContext.Current.CancellationToken);

        Assert.Equal(2, participants.Count);
        var participant = Assert.Single(participants, item => item.ConflictsWithSourceModule is null);
        Assert.Equal("20", participant.EventId);
        Assert.Equal("Restricted assignment", participant.Title);
        Assert.Equal(AssignmentStart, participant.Start);
        Assert.Equal(AssignmentEnd, participant.End);
        Assert.Equal(UserId, participant.ResourceId);
        Assert.Equal(50, participant.SourceEntityId);
        Assert.Equal("scheduling", participant.SourceModule);
        Assert.Equal("America/Vancouver", participant.TimeZoneId);
        var leaveConflictShift = Assert.Single(participants, item => item.ConflictsWithSourceModule == "leave");
        Assert.Equal("10", leaveConflictShift.EventId);
        Assert.Equal(
            await _provider.GetParticipantsAsync(CreateQuery(), TestContext.Current.CancellationToken),
            participants
        );
    }

    [Theory]
    [InlineData(false, "draft", false, 0, 1)]
    [InlineData(true, "draft", false, 0, 0)]
    [InlineData(false, "draft", true, 1, 1)]
    [InlineData(true, "draft", true, 1, 1)]
    [InlineData(false, "cancelled", true, 0, 1)]
    [InlineData(true, "cancelled", true, 0, 0)]
    public async Task GetParticipantsAsync_AuthorizedViewer_PreservesStatusFilters(
        bool updateShift,
        string status,
        bool includeDraft,
        int expectedCount,
        int expectedLeaveConflictShiftCount
    )
    {
        var eventEntity = await _db.Events.FindAsync([updateShift ? 10 : 20], TestContext.Current.CancellationToken);
        eventEntity!.StatusTypeCode = status;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var participants = await _provider.GetParticipantsAsync(
            CreateQuery() with
            {
                IncludeDraftParticipants = includeDraft,
            },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(expectedCount, participants.Count(item => item.ConflictsWithSourceModule is null));
        Assert.Equal(
            expectedLeaveConflictShiftCount,
            participants.Count(item => item.ConflictsWithSourceModule == "leave")
        );
    }

    [Fact]
    public async Task GetParticipantsAsync_AuthorizedViewer_PreservesResourceAndHalfOpenRangeFilters()
    {
        var query = CreateQuery();
        Assert.Single(
            (
                await _provider.GetParticipantsAsync(
                    query with
                    {
                        ResourceIds = [UserId],
                    },
                    TestContext.Current.CancellationToken
                )
            ).Where(item => item.ConflictsWithSourceModule is null)
        );
        Assert.Empty(
            await _provider.GetParticipantsAsync(
                query with
                {
                    ResourceIds = [Guid.NewGuid()],
                },
                TestContext.Current.CancellationToken
            )
        );
        Assert.Empty(
            (
                await _provider.GetParticipantsAsync(
                    query with
                    {
                        EndAtUtc = AssignmentStart,
                    },
                    TestContext.Current.CancellationToken
                )
            ).Where(item => item.ConflictsWithSourceModule is null)
        );
        Assert.Empty(
            (
                await _provider.GetParticipantsAsync(
                    query with
                    {
                        StartAtUtc = AssignmentEnd,
                    },
                    TestContext.Current.CancellationToken
                )
            ).Where(item => item.ConflictsWithSourceModule is null)
        );
    }

    [Theory]
    [InlineData("10")]
    [InlineData("20")]
    public async Task GetParticipantAsync_WhenEventIsShiftOrAssignment_ReconstructsParticipant(string eventId)
    {
        var participant = await _provider.GetParticipantAsync(
            new CalendarConflictEventIdentity(SchedulingConstants.SourceModule, eventId),
            UserId,
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(participant);
        Assert.Equal(eventId, participant.EventId);
        Assert.Equal(UserId, participant.ResourceId);
    }

    [Fact]
    public async Task GetParticipantsAsync_WithLocationScope_IncludesCrossLocationEventsForScopedResourcesOnly()
    {
        var vancouver = new Location
        {
            Id = 1,
            AgencyId = "VAN",
            Name = "Vancouver",
            Timezone = "America/Vancouver",
        };
        var toronto = new Location
        {
            Id = 2,
            AgencyId = "TOR",
            Name = "Toronto",
            Timezone = "America/Toronto",
        };
        _db.Locations.AddRange(vancouver, toronto);
        _db.ShiftAssignmentEntryUsers.AddRange(
            CreateLinkedParticipant(100, 101, MultiLocationUserId, vancouver),
            CreateLinkedParticipant(200, 201, MultiLocationUserId, toronto),
            CreateLinkedParticipant(300, 301, UnrelatedUserId, toronto)
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var participants = await _provider.GetParticipantsAsync(
            CreateQuery() with
            {
                LocationId = vancouver.Id,
                ResourceIds = [MultiLocationUserId, UnrelatedUserId],
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(
            new[] { "101", "201" },
            participants.Where(item => item.ConflictsWithSourceModule is null).Select(item => item.EventId).Order()
        );
        Assert.True(participants.Single(participant => participant.EventId == "101").IsInQueryScope);
        Assert.False(participants.Single(participant => participant.EventId == "201").IsInQueryScope);
        Assert.DoesNotContain(participants, participant => participant.ResourceId == UnrelatedUserId);
    }

    [Fact]
    public async Task GetParticipantsAsync_WithLocationScope_DetectsLocationlessAndCrossLocationConflict()
    {
        var vancouver = new Location
        {
            Id = 4,
            AgencyId = "VAN2",
            Name = "Vancouver 2",
            Timezone = "America/Vancouver",
        };
        var toronto = new Location
        {
            Id = 5,
            AgencyId = "TOR2",
            Name = "Toronto 2",
            Timezone = "America/Toronto",
        };
        _db.Locations.AddRange(vancouver, toronto);
        _db.ShiftAssignmentEntryUsers.AddRange(
            CreateLinkedParticipant(500, 501, MultiLocationUserId, null),
            CreateLinkedParticipant(600, 601, MultiLocationUserId, toronto),
            CreateLinkedParticipant(700, 701, UnrelatedUserId, toronto)
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var participants = await _provider.GetParticipantsAsync(
            CreateQuery() with
            {
                LocationId = vancouver.Id,
                ResourceIds = [MultiLocationUserId, UnrelatedUserId],
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(
            new[] { "501", "601" },
            participants.Where(item => item.ConflictsWithSourceModule is null).Select(item => item.EventId).Order()
        );
        Assert.True(participants.Single(participant => participant.EventId == "501").IsInQueryScope);
        Assert.False(participants.Single(participant => participant.EventId == "601").IsInQueryScope);
        Assert.Single(CalendarConflictDetector.Detect(participants));
        Assert.DoesNotContain(participants, participant => participant.ResourceId == UnrelatedUserId);
    }

    [Fact]
    public async Task GetParticipantsAsync_WithLocationScope_ReturnsLeaveOnlyShiftsForScopedResources()
    {
        var vancouver = new Location
        {
            Id = 6,
            AgencyId = "VAN3",
            Name = "Vancouver 3",
            Timezone = "America/Vancouver",
        };
        var toronto = new Location
        {
            Id = 7,
            AgencyId = "TOR3",
            Name = "Toronto 3",
            Timezone = "America/Toronto",
        };
        _db.Locations.AddRange(vancouver, toronto);
        _db.ShiftEntryUsers.AddRange(
            CreateShiftParticipant(1000, MultiLocationUserId, null),
            CreateShiftParticipant(1100, MultiLocationUserId, toronto),
            CreateShiftParticipant(1200, UnrelatedUserId, toronto)
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var participants = await _provider.GetParticipantsAsync(
            CreateQuery() with
            {
                LocationId = vancouver.Id,
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(new[] { "1000", "1100" }, participants.Select(participant => participant.EventId).Order());
        Assert.All(participants, participant => Assert.Equal("leave", participant.ConflictsWithSourceModule));
        Assert.True(participants.Single(participant => participant.EventId == "1000").IsInQueryScope);
        Assert.False(participants.Single(participant => participant.EventId == "1100").IsInQueryScope);
        Assert.DoesNotContain(participants, participant => participant.ResourceId == UnrelatedUserId);
    }

    [Fact]
    public async Task GetParticipantsForShiftEntriesAsync_WhenShiftHasLinkedAssignment_ReturnsAssignmentParticipant()
    {
        var participants = await SchedulingConflictParticipantProvider.GetParticipantsForShiftEntriesAsync(
            _db,
            [40],
            TestContext.Current.CancellationToken
        );

        var participant = Assert.Single(participants.Where(item => item.ConflictsWithSourceModule is null));
        Assert.Equal("20", participant.EventId);
        Assert.Equal(UserId, participant.ResourceId);
    }

    [Theory]
    [InlineData(CalendarEventStatusTypeCodes.Active, true)]
    [InlineData(CalendarEventStatusTypeCodes.Draft, false)]
    public async Task GetParticipantsForShiftEntriesAsync_WhenShiftHasNoAssignmentLinks_ReturnsPublishedLeaveOnlyCandidate(
        string status,
        bool expected
    )
    {
        var draftParticipant = CreateShiftParticipant(1300, MultiLocationUserId, null);
        draftParticipant.ShiftEntry!.Event!.StatusTypeCode = status;
        _db.ShiftEntryUsers.Add(draftParticipant);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var participants = await SchedulingConflictParticipantProvider.GetParticipantsForShiftEntriesAsync(
            _db,
            [1300],
            TestContext.Current.CancellationToken
        );

        if (expected)
        {
            var participant = Assert.Single(participants);
            Assert.Equal("leave", participant.ConflictsWithSourceModule);
            Assert.Equal("1300", participant.EventId);
        }
        else
            Assert.Empty(participants);
    }

    [Fact]
    public async Task GetParticipantsAsync_WhenTimeZonesAreMissing_DetectsUtcOverlap()
    {
        var first = CreateLinkedParticipant(800, 801, MultiLocationUserId, null);
        var second = CreateLinkedParticipant(900, 901, MultiLocationUserId, null);
        first.ShiftAssignmentEntry!.AssignmentEntry!.Event!.TimeZoneId = null;
        second.ShiftAssignmentEntry!.AssignmentEntry!.Event!.TimeZoneId = null;
        _db.ShiftAssignmentEntryUsers.AddRange(first, second);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var participants = await _provider.GetParticipantsAsync(
            CreateQuery() with
            {
                ResourceIds = [MultiLocationUserId],
            },
            TestContext.Current.CancellationToken
        );

        var assignments = participants.Where(item => item.ConflictsWithSourceModule is null).ToList();
        Assert.Equal(2, assignments.Count);
        Assert.All(assignments, participant => Assert.Null(participant.TimeZoneId));
        Assert.Single(CalendarConflictDetector.Detect(participants));
    }

    [Fact]
    public async Task GetParticipantsAsync_WhenLocationTimeZoneIsBlank_FallsBackToEventTimeZone()
    {
        var location = new Location
        {
            Id = 3,
            AgencyId = "BLANK",
            Name = "Blank time zone",
            Timezone = " ",
        };
        _db.Locations.Add(location);
        _db.ShiftAssignmentEntryUsers.Add(CreateLinkedParticipant(400, 401, MultiLocationUserId, location));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var participants = await _provider.GetParticipantsAsync(
            CreateQuery() with
            {
                ResourceIds = [MultiLocationUserId],
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(
            "America/Vancouver",
            Assert.Single(participants, item => item.ConflictsWithSourceModule is null).TimeZoneId
        );
    }

    private static CalendarConflictQuery CreateQuery() =>
        new(
            new DateTimeOffset(2026, 8, 24, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 25, 0, 0, 0, TimeSpan.Zero)
        );

    private static Event CreateEvent(int id, string title, string eventTypeCode) =>
        new()
        {
            Id = id,
            Title = title,
            StartAtUtc = new DateTimeOffset(2026, 8, 24, 16, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2026, 8, 24, 20, 0, 0, TimeSpan.Zero),
            EventTypeCode = eventTypeCode,
            StatusTypeCode = CalendarEventStatusTypeCodes.Active,
            SourceModule = SchedulingConstants.SourceModule,
            TimeZoneId = "America/Vancouver",
        };

    private static ShiftAssignmentEntryUser CreateLinkedParticipant(
        int id,
        int assignmentEventId,
        Guid userId,
        Location? location
    )
    {
        var shiftEvent = CreateEvent(id, $"Shift {id}", SchedulingConstants.ShiftEventTypeCode);
        var assignmentEvent = CreateEvent(
            assignmentEventId,
            $"Assignment {assignmentEventId}",
            SchedulingConstants.AssignmentEventTypeCode
        );
        if (location is not null)
        {
            shiftEvent.Location = location;
            shiftEvent.LocationId = location.Id;
            assignmentEvent.Location = location;
            assignmentEvent.LocationId = location.Id;
        }
        return new ShiftAssignmentEntryUser
        {
            Id = id,
            UserId = userId,
            ShiftAssignmentEntry = new ShiftAssignmentEntry
            {
                Id = id,
                ShiftEntryId = id,
                ShiftEntry = new ShiftEntry
                {
                    Id = id,
                    EventId = shiftEvent.Id,
                    Event = shiftEvent,
                    Users = [new ShiftEntryUser { UserId = userId }],
                },
                AssignmentEntryId = id,
                AssignmentEntry = new AssignmentEntry
                {
                    Id = id,
                    EventId = assignmentEvent.Id,
                    Event = assignmentEvent,
                    AssignmentDefinitionId = 1,
                    CategoryId = 1,
                    SubCategoryId = 1,
                    Capacity = 1,
                },
            },
        };
    }

    private static ShiftEntryUser CreateShiftParticipant(int eventId, Guid userId, Location? location)
    {
        var shiftEvent = CreateEvent(eventId, $"Shift {eventId}", SchedulingConstants.ShiftEventTypeCode);
        if (location is not null)
        {
            shiftEvent.Location = location;
            shiftEvent.LocationId = location.Id;
        }

        return new ShiftEntryUser
        {
            Id = eventId,
            UserId = userId,
            ShiftEntry = new ShiftEntry
            {
                Id = eventId,
                EventId = shiftEvent.Id,
                Event = shiftEvent,
            },
        };
    }
}
