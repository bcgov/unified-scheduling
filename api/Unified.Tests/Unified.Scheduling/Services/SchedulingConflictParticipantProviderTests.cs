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
using Unified.Db.Models.LoanOut;
using Unified.Db.Models.Scheduling;
using Unified.Db.Models.UserManagement;
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
        _provider = new SchedulingConflictParticipantProvider(
            _db,
            SchedulingLoanOutTestServices.CreateAvailability(_db, new TimeZoneService())
        );

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
        var service = new CalendarConflictService(
            [_provider, trainingProvider],
            _db,
            projection,
            CalendarConflictTestRules.Scheduling
        );
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
    [InlineData(true, false, nameof(Permissions.AssignmentsView), "20")]
    [InlineData(false, true, nameof(Permissions.AssignmentsView), null)]
    [InlineData(true, false, nameof(Permissions.ShiftsView), "10")]
    [InlineData(false, true, nameof(Permissions.ShiftsView), null)]
    [InlineData(true, false, nameof(Permissions.UserTrainingsView), null)]
    [InlineData(false, true, nameof(Permissions.UserTrainingsView), "training.user-training.77")]
    [InlineData(true, true, nameof(Permissions.UserTrainingsView), "training.user-training.77")]
    [InlineData(true, true, "", null)]
    public async Task ConflictProjection_RestrictedWarnings_RespectReportingDirectionAndPermissions(
        bool reportOnEntry,
        bool reportOnOverlaps,
        string permission,
        string? expectedAnchor
    )
    {
        var ct = TestContext.Current.CancellationToken;
        var trainingProvider = await SeedTrainingAsync();
        var assignment = await _provider.GetParticipantAsync(new("scheduling", "20"), UserId, ct);
        var training = await trainingProvider.GetParticipantAsync(
            new("training", "training.user-training.77"),
            UserId,
            ct
        );
        var conflict = new CalendarConflict(
            assignment!,
            training!,
            UserId,
            AssignmentStart,
            AssignmentEnd,
            ReportOnEntry: reportOnEntry,
            ReportOnOverlaps: reportOnOverlaps
        );
        var projection = new CalendarConflictProjection([_provider, trainingProvider]);
        var result = await projection.ProjectAsync(
            [conflict],
            CalendarConflictTestContext.Viewer(UserId, permission),
            ct
        );

        Assert.Empty(result.Conflicts);
        Assert.True(result.HasRestrictedConflicts);
        if (expectedAnchor is null)
            Assert.Empty(result.RestrictedWarnings);
        else
            Assert.Equal(expectedAnchor, Assert.Single(result.RestrictedWarnings).EventId);

        // Reporting controls warning anchors; it must not weaken enforcement or alter detailed responses.
        var visible = await projection.ProjectAsync(
            [conflict],
            CalendarConflictTestContext.Viewer(
                UserId,
                nameof(Permissions.AssignmentsView),
                nameof(Permissions.UserTrainingsView)
            ),
            ct
        );
        var detail = Assert.Single(visible.Conflicts);
        Assert.Equal(reportOnEntry, detail.ReportOnEntry);
        Assert.Equal(reportOnOverlaps, detail.ReportOnOverlaps);
        Assert.Empty(visible.RestrictedWarnings);
        Assert.False(visible.HasRestrictedConflicts);
        var approved = await projection.ProjectAsync(
            [conflict with { IsOverridden = true }],
            CalendarConflictTestContext.Viewer(UserId, permission),
            ct
        );
        Assert.Empty(approved.RestrictedWarnings);
        Assert.False(approved.HasRestrictedConflicts);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ConflictOverride_WithoutUnderlyingVisibility_RejectsDirectAndForgedAcknowledgements(
        bool acknowledgement,
        bool trainingOnly
    )
    {
        var training = await SeedTrainingAsync();
        var accessor = CalendarConflictTestContext.Accessor(UserId);
        accessor.HttpContext!.User = CalendarConflictTestContext.Viewer(
            UserId,
            nameof(Permissions.CalendarConflictsOverride),
            trainingOnly ? nameof(Permissions.UserTrainingsView) : nameof(Permissions.AssignmentsView)
        );
        var projection = new CalendarConflictProjection([_provider, training], accessor);
        var service = new CalendarConflictService(
            [_provider, training],
            _db,
            projection,
            CalendarConflictTestRules.Scheduling
        );
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
        var service = new CalendarConflictService(
            [_provider, training],
            _db,
            projection,
            CalendarConflictTestRules.Scheduling
        );
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
        var service = new CalendarConflictService(
            [_provider, training],
            _db,
            projection,
            CalendarConflictTestRules.Scheduling
        );
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
        var participant = Assert.Single(
            participants,
            item => item.EventTypeCode == SchedulingConstants.AssignmentEventTypeCode
        );
        Assert.Equal("20", participant.EventId);
        Assert.Equal("Restricted assignment", participant.Title);
        Assert.Equal(AssignmentStart, participant.Start);
        Assert.Equal(AssignmentEnd, participant.End);
        Assert.Equal(UserId, participant.ResourceId);
        Assert.Equal(50, participant.SourceEntityId);
        Assert.Equal("scheduling", participant.SourceModule);
        Assert.Equal("America/Vancouver", participant.TimeZoneId);
        var leaveConflictShift = Assert.Single(
            participants,
            item => item.EventTypeCode == SchedulingConstants.ShiftEventTypeCode
        );
        Assert.Equal("10", leaveConflictShift.EventId);
        Assert.Equal("Shift", leaveConflictShift.Title);
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
        Assert.Equal(
            expectedCount,
            participants.Count(item => item.EventTypeCode == SchedulingConstants.AssignmentEventTypeCode)
        );
        Assert.Equal(
            expectedLeaveConflictShiftCount,
            participants.Count(item => item.EventTypeCode == SchedulingConstants.ShiftEventTypeCode)
        );
    }

    [Fact]
    public async Task GetParticipantsAsync_AuthorizedViewer_PreservesResourceAndHalfOpenRangeFilters()
    {
        var query = CreateQuery();
        Assert.Single(
            await _provider.GetParticipantsAsync(
                query with
                {
                    ResourceIds = [UserId],
                },
                TestContext.Current.CancellationToken
            ),
            item => item.EventTypeCode == SchedulingConstants.AssignmentEventTypeCode
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
        Assert.DoesNotContain(
            await _provider.GetParticipantsAsync(
                query with
                {
                    EndAtUtc = AssignmentStart,
                },
                TestContext.Current.CancellationToken
            ),
            item => item.EventTypeCode == SchedulingConstants.AssignmentEventTypeCode
        );
        Assert.DoesNotContain(
            await _provider.GetParticipantsAsync(
                query with
                {
                    StartAtUtc = AssignmentEnd,
                },
                TestContext.Current.CancellationToken
            ),
            item => item.EventTypeCode == SchedulingConstants.AssignmentEventTypeCode
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
        Assert.Equal(
            eventId == "10" ? SchedulingConstants.ShiftEventTypeCode : SchedulingConstants.AssignmentEventTypeCode,
            participant.EventTypeCode
        );
        Assert.Equal(UserId, participant.ResourceId);
    }

    [Fact]
    public async Task GetParticipantsForPublicationAsync_ShiftAndAssignment_KeepDistinctTypes()
    {
        var participants = await SchedulingConflictParticipantProvider.GetParticipantsForPublicationAsync(
            _db,
            SchedulingLoanOutTestServices.CreateAvailability(_db, new TimeZoneService()),
            [40],
            [50],
            TestContext.Current.CancellationToken
        );

        Assert.Equal(2, participants.Count);
        Assert.All(
            participants,
            participant => Assert.Equal(SchedulingConstants.SourceModule, participant.SourceModule)
        );
        Assert.Equal(
            SchedulingConstants.ShiftEventTypeCode,
            participants.Single(participant => participant.EventId == "10").EventTypeCode
        );
        Assert.Equal(
            SchedulingConstants.AssignmentEventTypeCode,
            participants.Single(participant => participant.EventId == "20").EventTypeCode
        );
    }

    [Fact]
    public async Task GetParticipantsAsync_WithLocationScope_DefersCrossLocationResourceFiltering()
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
            new[] { "101", "201", "301" },
            participants
                .Where(item => item.EventTypeCode == SchedulingConstants.AssignmentEventTypeCode)
                .Select(item => item.EventId)
                .Order()
        );
        Assert.True(participants.Single(participant => participant.EventId == "101").IsInQueryScope);
        Assert.False(participants.Single(participant => participant.EventId == "201").IsInQueryScope);
        Assert.All(
            participants.Where(participant => participant.ResourceId == UnrelatedUserId),
            participant => Assert.False(participant.IsInQueryScope)
        );
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
            new[] { "501", "601", "701" },
            participants
                .Where(item => item.EventTypeCode == SchedulingConstants.AssignmentEventTypeCode)
                .Select(item => item.EventId)
                .Order()
        );
        Assert.True(participants.Single(participant => participant.EventId == "501").IsInQueryScope);
        Assert.False(participants.Single(participant => participant.EventId == "601").IsInQueryScope);
        var service = new CalendarConflictService(
            [_provider],
            _db,
            new CalendarConflictProjection([_provider]),
            CalendarConflictTestRules.Scheduling
        );
        Assert.Single(
            await service.GetConflictsAsync(
                CreateQuery() with
                {
                    LocationId = vancouver.Id,
                    ResourceIds = [MultiLocationUserId, UnrelatedUserId],
                },
                TestContext.Current.CancellationToken
            )
        );
        Assert.All(
            participants.Where(participant => participant.ResourceId == UnrelatedUserId),
            participant => Assert.False(participant.IsInQueryScope)
        );
    }

    [Fact]
    public async Task GetParticipantsAsync_WithLocationScope_ReturnsSupplementalShiftsAcrossLocations()
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
                ResourceIds = [MultiLocationUserId, UnrelatedUserId],
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(new[] { "1000", "1100", "1200" }, participants.Select(participant => participant.EventId).Order());
        Assert.All(
            participants,
            participant => Assert.Equal(SchedulingConstants.ShiftEventTypeCode, participant.EventTypeCode)
        );
        Assert.True(participants.Single(participant => participant.EventId == "1000").IsInQueryScope);
        Assert.False(participants.Single(participant => participant.EventId == "1100").IsInQueryScope);
        Assert.False(participants.Single(participant => participant.EventId == "1200").IsInQueryScope);
    }

    [Fact]
    public async Task GetParticipantsForShiftEntriesAsync_WhenShiftHasLinkedAssignment_ReturnsAssignmentParticipant()
    {
        var participants = await SchedulingConflictParticipantProvider.GetParticipantsForShiftEntriesAsync(
            _db,
            SchedulingLoanOutTestServices.CreateAvailability(_db, new TimeZoneService()),
            [40],
            TestContext.Current.CancellationToken
        );

        var participant = Assert.Single(
            participants,
            item => item.EventTypeCode == SchedulingConstants.AssignmentEventTypeCode
        );
        Assert.Equal("20", participant.EventId);
        Assert.Equal(UserId, participant.ResourceId);
    }

    [Theory]
    [InlineData(CalendarEventStatusTypeCodes.Active, true)]
    [InlineData(CalendarEventStatusTypeCodes.Draft, false)]
    public async Task GetParticipantsForShiftEntriesAsync_WhenShiftHasNoAssignmentLinks_ReturnsPublishedShiftCandidate(
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
            SchedulingLoanOutTestServices.CreateAvailability(_db, new TimeZoneService()),
            [1300],
            TestContext.Current.CancellationToken
        );

        if (expected)
        {
            var participant = Assert.Single(participants);
            Assert.Equal(SchedulingConstants.ShiftEventTypeCode, participant.EventTypeCode);
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

        var assignments = participants
            .Where(item => item.EventTypeCode == SchedulingConstants.AssignmentEventTypeCode)
            .ToList();
        Assert.Equal(2, assignments.Count);
        Assert.All(assignments, participant => Assert.Null(participant.TimeZoneId));
        Assert.Single(CalendarConflictDetector.Detect(assignments));
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
            Assert
                .Single(participants, item => item.EventTypeCode == SchedulingConstants.AssignmentEventTypeCode)
                .TimeZoneId
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetParticipantsAsync_LoanedOutUser_ExcludesOnlySourceParticipationAcrossAllChecks(bool mixedShift)
    {
        var source = await SeedLoanOutAsync();
        if (mixedShift)
        {
            source.ShiftAssignmentEntry!.ShiftEntry!.Users.Add(new ShiftEntryUser { UserId = UnrelatedUserId });
            _db.ShiftAssignmentEntryUsers.Add(
                new ShiftAssignmentEntryUser
                {
                    ShiftAssignmentEntry = source.ShiftAssignmentEntry,
                    UserId = UnrelatedUserId,
                }
            );
            await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var availability = SchedulingLoanOutTestServices.CreateAvailability(_db, new TimeZoneService());
        var participants = await _provider.GetParticipantsAsync(CreateQuery(), TestContext.Current.CancellationToken);
        Assert.DoesNotContain(
            participants,
            item => item.ResourceId == MultiLocationUserId && (item.EventId == "300" || item.EventId == "301")
        );
        Assert.Contains(participants, item => item.ResourceId == MultiLocationUserId && item.EventId == "400");
        Assert.Contains(
            participants,
            item => item.ResourceId == MultiLocationUserId && item.EventId == "401" && item.SourceEntityId == 400
        );
        Assert.Equal(mixedShift, participants.Any(item => item.ResourceId == UnrelatedUserId && item.EventId == "300"));
        Assert.Equal(mixedShift, participants.Any(item => item.ResourceId == UnrelatedUserId && item.EventId == "301"));

        var publication = await SchedulingConflictParticipantProvider.GetParticipantsForPublicationAsync(
            _db,
            availability,
            [300, 400],
            [300, 400],
            TestContext.Current.CancellationToken
        );
        var shiftChecks = await SchedulingConflictParticipantProvider.GetParticipantsForShiftEntriesAsync(
            _db,
            availability,
            [300, 400],
            TestContext.Current.CancellationToken
        );
        var entryChecks = await SchedulingConflictParticipantProvider.GetParticipantsForAssignmentEntriesAsync(
            _db,
            availability,
            [300, 400],
            TestContext.Current.CancellationToken
        );
        var seriesChecks = await SchedulingConflictParticipantProvider.GetParticipantsForAssignmentSeriesAsync(
            _db,
            availability,
            77,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(
            publication.OrderBy(item => item.EventId).ThenBy(item => item.ResourceId),
            shiftChecks.OrderBy(item => item.EventId).ThenBy(item => item.ResourceId)
        );
        Assert.Equal(
            entryChecks.OrderBy(item => item.EventId).ThenBy(item => item.ResourceId),
            seriesChecks.OrderBy(item => item.EventId).ThenBy(item => item.ResourceId)
        );
        Assert.DoesNotContain(
            publication.Concat(entryChecks),
            item => item.ResourceId == MultiLocationUserId && (item.EventId == "300" || item.EventId == "301")
        );
        Assert.Contains(publication, item => item.ResourceId == MultiLocationUserId && item.EventId == "401");
        foreach (var eventId in new[] { "300", "301", "400", "401" })
        {
            var participant = await _provider.GetParticipantAsync(
                new CalendarConflictEventIdentity(SchedulingConstants.SourceModule, eventId),
                MultiLocationUserId,
                TestContext.Current.CancellationToken
            );
            Assert.Equal(eventId is "300" or "301", participant is null);
        }

        var sourceScoped = await _provider.GetParticipantsAsync(
            CreateQuery() with
            {
                LocationId = 30,
            },
            TestContext.Current.CancellationToken
        );
        Assert.DoesNotContain(sourceScoped, item => item.ResourceId == MultiLocationUserId && item.IsInQueryScope);
        var destinationScoped = await _provider.GetParticipantsAsync(
            CreateQuery() with
            {
                LocationId = 40,
            },
            TestContext.Current.CancellationToken
        );
        Assert.Contains(
            destinationScoped,
            item => item.ResourceId == MultiLocationUserId && item.EventId == "401" && item.IsInQueryScope
        );
    }

    [Theory]
    [InlineData(true, CalendarEventStatusTypeCodes.Active, 16, 20, true)]
    [InlineData(true, CalendarEventStatusTypeCodes.Active, 22, 23, true)]
    [InlineData(true, CalendarEventStatusTypeCodes.Active, 0, 7, false)]
    [InlineData(true, CalendarEventStatusTypeCodes.Cancelled, 16, 20, false)]
    [InlineData(false, CalendarEventStatusTypeCodes.Active, 16, 20, false)]
    public async Task GetParticipantsAsync_LoanOutEligibility_ReusesLocalDayAndFeatureRules(
        bool enabled,
        string status,
        int startHour,
        int endHour,
        bool excluded
    )
    {
        await SeedLoanOutAsync();
        var loanOut = await _db
            .LoanOutEntries.Include(entry => entry.Event)
            .SingleAsync(TestContext.Current.CancellationToken);
        loanOut.Event!.StatusTypeCode = status;
        loanOut.Event.StartAtUtc = new DateTimeOffset(2026, 8, 24, startHour, 0, 0, TimeSpan.Zero);
        loanOut.Event.EndAtUtc = new DateTimeOffset(2026, 8, 24, endHour, 0, 0, TimeSpan.Zero);
        var nextDay = CreateLinkedParticipant(
            500,
            501,
            MultiLocationUserId,
            await _db.Locations.FindAsync([30], TestContext.Current.CancellationToken)
        );
        nextDay.ShiftAssignmentEntry!.ShiftEntry!.Event!.StartAtUtc =
            nextDay.ShiftAssignmentEntry.ShiftEntry.Event.StartAtUtc.AddDays(1);
        nextDay.ShiftAssignmentEntry.ShiftEntry.Event.EndAtUtc =
            nextDay.ShiftAssignmentEntry.ShiftEntry.Event.EndAtUtc!.Value.AddDays(1);
        nextDay.ShiftAssignmentEntry.AssignmentEntry!.Event!.StartAtUtc =
            nextDay.ShiftAssignmentEntry.AssignmentEntry.Event.StartAtUtc.AddDays(1);
        nextDay.ShiftAssignmentEntry.AssignmentEntry.Event.EndAtUtc =
            nextDay.ShiftAssignmentEntry.AssignmentEntry.Event.EndAtUtc!.Value.AddDays(1);
        _db.ShiftAssignmentEntryUsers.Add(nextDay);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var availability = SchedulingLoanOutTestServices.CreateAvailability(
            _db,
            new TimeZoneService(),
            loanOutEnabled: enabled
        );
        var provider = new SchedulingConflictParticipantProvider(_db, availability);
        var participants = await provider.GetParticipantsAsync(
            CreateQuery() with
            {
                EndAtUtc = CreateQuery().EndAtUtc.AddDays(1),
            },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(
            !excluded,
            participants.Any(item => item.ResourceId == MultiLocationUserId && item.EventId == "300")
        );
        Assert.Equal(
            !excluded,
            participants.Any(item => item.ResourceId == MultiLocationUserId && item.EventId == "301")
        );
        Assert.Contains(participants, item => item.ResourceId == MultiLocationUserId && item.EventId == "501");
        Assert.Contains(participants, item => item.ResourceId == MultiLocationUserId && item.EventId == "401");
        var publication = await SchedulingConflictParticipantProvider.GetParticipantsForPublicationAsync(
            _db,
            availability,
            [300],
            [],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(!excluded, publication.Any(item => item.EventId == "301"));
        var lookup = await provider.GetParticipantAsync(
            new CalendarConflictEventIdentity(SchedulingConstants.SourceModule, "301"),
            MultiLocationUserId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(excluded, lookup is null);
    }

    [Fact]
    public async Task GetParticipantAsync_UserBecomesLoanedOut_InvalidatesSourceConflictOverride()
    {
        await SeedLoanOutAsync();
        var loanOut = await _db
            .LoanOutEntries.Include(entry => entry.Event)
            .SingleAsync(TestContext.Current.CancellationToken);
        loanOut.Event!.StatusTypeCode = CalendarEventStatusTypeCodes.Cancelled;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var conflicts = new CalendarConflictService(
            [_provider],
            _db,
            CalendarConflictTestContext.Projection(UserId),
            CalendarConflictTestRules.Scheduling
        );
        await conflicts.CreateOverrideAsync(
            new CalendarConflictAcknowledgement
            {
                FirstSourceModule = SchedulingConstants.SourceModule,
                FirstEventId = "301",
                SecondSourceModule = SchedulingConstants.SourceModule,
                SecondEventId = "401",
                ResourceId = MultiLocationUserId,
                Note = "Approved overlap",
            },
            UserId,
            TestContext.Current.CancellationToken
        );
        var conflictOverride = await _db.CalendarConflictOverrides.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Null(conflictOverride.InvalidatedOn);
        loanOut.Event.StatusTypeCode = CalendarEventStatusTypeCodes.Active;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await conflicts.InvalidateResolvedOverridesAsync(
            [new CalendarConflictEventIdentity(SchedulingConstants.SourceModule, "301")],
            UserId,
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(conflictOverride.InvalidatedOn);
        Assert.NotNull(
            await _provider.GetParticipantAsync(
                new CalendarConflictEventIdentity(SchedulingConstants.SourceModule, "401"),
                MultiLocationUserId,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task GetParticipantsAsync_LoanOutWithoutSourceLocation_UsesEmployeeHomeLocation()
    {
        await SeedLoanOutAsync();
        var loanOut = await _db
            .LoanOutEntries.Include(entry => entry.Event)
            .SingleAsync(TestContext.Current.CancellationToken);
        loanOut.Event!.Location = null;
        loanOut.Event.LocationId = null;
        _db.Users.Add(
            new User
            {
                Id = MultiLocationUserId,
                IdirName = "loaned-user",
                HomeLocationId = 30,
            }
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var participants = await _provider.GetParticipantsAsync(CreateQuery(), TestContext.Current.CancellationToken);
        Assert.DoesNotContain(
            participants,
            item => item.ResourceId == MultiLocationUserId && (item.EventId == "300" || item.EventId == "301")
        );
        Assert.Contains(participants, item => item.ResourceId == MultiLocationUserId && item.EventId == "401");
    }

    [Fact]
    public async Task GetParticipantsAsync_CancelledRequest_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _provider.GetParticipantsAsync(CreateQuery(), cancellation.Token)
        );
    }

    [Fact]
    public async Task GetParticipantsForPublicationAsync_DraftSourceShift_ExcludesLoanedOutUser()
    {
        var source = await SeedLoanOutAsync();
        source.ShiftAssignmentEntry!.ShiftEntry!.Event!.StatusTypeCode = CalendarEventStatusTypeCodes.Draft;
        source.ShiftAssignmentEntry.AssignmentEntry!.Event!.StatusTypeCode = CalendarEventStatusTypeCodes.Draft;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var participants = await SchedulingConflictParticipantProvider.GetParticipantsForPublicationAsync(
            _db,
            SchedulingLoanOutTestServices.CreateAvailability(_db, new TimeZoneService()),
            [300, 400],
            [],
            TestContext.Current.CancellationToken
        );
        Assert.DoesNotContain(participants, item => item.EventId == "300" || item.EventId == "301");
        Assert.Contains(participants, item => item.EventId == "401");
    }

    private async Task<ShiftAssignmentEntryUser> SeedLoanOutAsync()
    {
        var sourceLocation = new Location
        {
            Id = 30,
            AgencyId = "SOURCE",
            Name = "Source",
            Timezone = "America/Vancouver",
        };
        var destinationLocation = new Location
        {
            Id = 40,
            AgencyId = "DEST",
            Name = "Destination",
            Timezone = "America/Toronto",
        };
        var source = CreateLinkedParticipant(300, 301, MultiLocationUserId, sourceLocation);
        var destination = CreateLinkedParticipant(400, 401, MultiLocationUserId, destinationLocation);
        source.ShiftAssignmentEntry!.AssignmentEntry!.AssignmentSeriesId = 77;
        destination.ShiftAssignmentEntry!.AssignmentEntry!.AssignmentSeriesId = 77;
        _db.ShiftAssignmentEntryUsers.AddRange(source, destination);
        _db.LoanOutEntries.Add(
            new LoanOutEntry
            {
                LoanOutLocationId = destinationLocation.Id,
                Event = new Event
                {
                    Title = "Loan out",
                    SourceModule = "loanout",
                    EventTypeCode = "loanout",
                    StatusTypeCode = CalendarEventStatusTypeCodes.Active,
                    Location = sourceLocation,
                    StartAtUtc = new DateTimeOffset(2026, 8, 24, 7, 0, 0, TimeSpan.Zero),
                    EndAtUtc = new DateTimeOffset(2026, 8, 25, 7, 0, 0, TimeSpan.Zero),
                    TimeZoneId = "America/Vancouver",
                },
                Users = [new LoanOutEntryUser { UserId = MultiLocationUserId }],
            }
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return source;
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
