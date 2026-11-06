using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Unified.Authorization;
using Unified.Calendar.Conflicts;
using Unified.Calendar.Models;
using Unified.Common.Calendar.Conflicts;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Leave;
using Unified.Db.Models.Scheduling;
using Unified.Leave.Conflicts;
using Unified.Scheduling.Services;
using Unified.Tests.TestHelpers;

namespace Unified.Tests.Leave.Conflicts;

public sealed class LeaveConflictParticipantProviderTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Start = new(2026, 6, 1, 7, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserA = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserB = new("22222222-2222-2222-2222-222222222222");
    private UnifiedDbContext _db = null!;
    private LeaveConflictParticipantProvider _provider = null!;
    private CalendarConflictService _conflicts = null!;

    public ValueTask InitializeAsync()
    {
        _db = new UnifiedDbContext(
            new DbContextOptionsBuilder<UnifiedDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options
        );
        _provider = new LeaveConflictParticipantProvider(_db);
        ICalendarConflictParticipantProvider[] providers = [_provider, new SchedulingConflictParticipantProvider(_db)];
        _conflicts = new CalendarConflictService(providers, _db, new CalendarConflictProjection(providers));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _db.DisposeAsync();

    [Theory]
    [InlineData(true, false, true, true)]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, false, false)]
    public async Task CreateOverrideAsync_WithRealProviders_RequiresLeaveVisibilityAndOverridePermission(
        bool canViewLeave,
        bool canViewUserLeave,
        bool canOverride,
        bool expected
    )
    {
        var leave = await AddLeaveAsync();
        var shift = await AddShiftAsync(Start.AddHours(1), Start.AddHours(2));
        var permissions = new List<string> { nameof(Permissions.ShiftsView) };
        if (canViewLeave)
            permissions.Add(nameof(Permissions.LeaveView));
        if (canViewUserLeave)
            permissions.Add(nameof(Permissions.UsersLeaveView));
        if (canOverride)
            permissions.Add(nameof(Permissions.CalendarConflictsOverride));
        var accessor = CalendarConflictTestContext.Accessor(UserB);
        accessor.HttpContext!.User = CalendarConflictTestContext.Viewer(UserB, permissions.ToArray());
        ICalendarConflictParticipantProvider[] providers = [_provider, new SchedulingConflictParticipantProvider(_db)];
        var projection = new CalendarConflictProjection(providers, accessor);
        var service = new CalendarConflictService(providers, _db, projection);
        var projected = await projection.ProjectAsync(
            await service.GetConflictsAsync(Query(), TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken
        );
        Assert.Equal(canViewLeave ? 1 : 0, projected.Conflicts.Count);
        Assert.Equal(!canViewLeave, projected.HasRestrictedConflicts);
        Assert.All(projected.RestrictedWarnings, warning => Assert.Equal("scheduling", warning.SourceModule));
        var json = JsonSerializer.Serialize(projected);
        Assert.DoesNotContain("Restricted leave type", json);
        Assert.DoesNotContain("Private description", json);
        Assert.DoesNotContain("Private notes", json);
        if (canViewLeave)
            Assert.Contains("Leave", json);
        var acknowledgement = new CalendarConflictAcknowledgement
        {
            FirstSourceModule = "leave",
            FirstEventId = leave.EventId.ToString(),
            SecondSourceModule = "scheduling",
            SecondEventId = shift.EventId.ToString(),
            ResourceId = UserA,
            Note = "Approved overlap",
        };

        if (expected)
        {
            await service.CreateOverrideAsync(acknowledgement, UserB, TestContext.Current.CancellationToken);
            Assert.True(
                Assert
                    .Single(await service.GetConflictsAsync(Query(), TestContext.Current.CancellationToken))
                    .IsOverridden
            );
        }
        else
        {
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                service.CreateOverrideAsync(acknowledgement, UserB, TestContext.Current.CancellationToken)
            );
            Assert.Empty(_db.CalendarConflictOverrides);
        }
    }

    [Theory]
    [InlineData("leave", "0")]
    [InlineData("leave", "01")]
    [InlineData("leave", "not-an-id")]
    [InlineData("leave", "999")]
    [InlineData("scheduling", "1")]
    public async Task GetVisibilityAsync_WhenIdentityIsInvalid_ReturnsNull(string module, string eventId)
    {
        await AddLeaveAsync();
        var participant = Assert.Single(
            await _provider.GetParticipantsAsync(Query(), TestContext.Current.CancellationToken)
        );
        Assert.Null(
            await _provider.GetVisibilityAsync(
                participant with
                {
                    SourceModule = module,
                    EventId = eventId,
                },
                new HashSet<string> { nameof(Permissions.LeaveView) },
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task GetVisibilityAsync_WhenCanonicalLeave_ReturnsNoSchedulingAnchorsAndChecksMembership()
    {
        var leave = await AddLeaveAsync();
        var participant = Assert.Single(
            await _provider.GetParticipantsAsync(Query(), TestContext.Current.CancellationToken)
        );
        var permissions = new HashSet<string> { nameof(Permissions.LeaveView) };
        var visibility = await _provider.GetVisibilityAsync(
            participant,
            permissions,
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(visibility);
        Assert.True(visibility.CanView);
        Assert.Empty(visibility.VisibleAnchors);
        Assert.Null(
            await _provider.GetVisibilityAsync(
                participant with
                {
                    ResourceId = UserB,
                },
                permissions,
                TestContext.Current.CancellationToken
            )
        );
        leave.Event!.EventTypeCode = "general";
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Null(
            await _provider.GetVisibilityAsync(participant, permissions, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task GetParticipantsAsync_WhenDraftExcluded_StillIncludesRecordedLeaveWithoutViewPermissions()
    {
        var leave = await AddLeaveAsync();
        var participants = await _provider.GetParticipantsAsync(
            Query() with
            {
                IncludeDraftParticipants = false,
                LocationId = 9,
            },
            TestContext.Current.CancellationToken
        );
        var participant = Assert.Single(participants);
        Assert.Equal(LeaveConflictParticipantProvider.IdentityFor(leave.EventId), participant.Identity);
        Assert.Equal(leave.Id, participant.SourceEntityId);
        Assert.Equal(UserA, participant.ResourceId);
        Assert.Equal("Leave", participant.Title);
        Assert.True(participant.AllDay);
        Assert.False(participant.IsInQueryScope);
        Assert.False(participant.RequiresMatchingTimeZone);
    }

    [Theory]
    [InlineData("leave", "0")]
    [InlineData("leave", "01")]
    [InlineData("leave", "not-an-id")]
    [InlineData("scheduling", "1")]
    public async Task GetParticipantAsync_WhenIdentityIsNotCanonical_ReturnsNull(string module, string eventId)
    {
        await AddLeaveAsync();
        Assert.Null(
            await _provider.GetParticipantAsync(new(module, eventId), UserA, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task GetParticipantAsync_WhenMembershipChangesOrLeaveIsCancelled_ReturnsNull()
    {
        var leave = await AddLeaveAsync();
        var identity = LeaveConflictParticipantProvider.IdentityFor(leave.EventId);
        Assert.NotNull(await _provider.GetParticipantAsync(identity, UserA, TestContext.Current.CancellationToken));
        Assert.Null(await _provider.GetParticipantAsync(identity, UserB, TestContext.Current.CancellationToken));
        leave.Users.Single().UserId = UserB;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Null(await _provider.GetParticipantAsync(identity, UserA, TestContext.Current.CancellationToken));
        leave.Event!.StatusTypeCode = CalendarEventStatusTypeCodes.Cancelled;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Null(await _provider.GetParticipantAsync(identity, UserB, TestContext.Current.CancellationToken));
        Assert.Empty(await _provider.GetParticipantsAsync(Query(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetParticipantsAsync_WhenRangeOrUserDoesNotOverlap_ExcludesLeave()
    {
        await AddLeaveAsync();
        Assert.Empty(
            await _provider.GetParticipantsAsync(
                Query() with
                {
                    ResourceIds = [UserB],
                },
                TestContext.Current.CancellationToken
            )
        );
        Assert.Empty(
            await _provider.GetParticipantsAsync(
                new(Start.AddDays(1), Start.AddDays(2)),
                TestContext.Current.CancellationToken
            )
        );
        Assert.Empty(
            await _provider.GetParticipantsAsync(new(Start.AddDays(-1), Start), TestContext.Current.CancellationToken)
        );
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(22)]
    public async Task GetConflictsForCandidatesAsync_WhenAllDayLeaveOverlapsAnyShiftHour_BlocksPublication(int hours)
    {
        await AddLeaveAsync();
        var shift = await AddShiftAsync(Start.AddHours(hours), Start.AddHours(hours + 1));
        var candidates = await SchedulingConflictParticipantProvider.GetParticipantsForPublicationAsync(
            _db,
            [shift.Id],
            [],
            TestContext.Current.CancellationToken
        );
        Assert.Single(candidates);
        await Assert.ThrowsAsync<CalendarConflictException>(() =>
            _conflicts.EnsureNoUnresolvedConflictsAsync(candidates, TestContext.Current.CancellationToken)
        );
        Assert.Equal(CalendarEventStatusTypeCodes.Draft, shift.Event!.StatusTypeCode);
    }

    [Fact]
    public async Task GetConflictsForCandidatesAsync_WhenAssignmentOverlapsLeave_BlocksPublication()
    {
        await AddLeaveAsync();
        var shift = await AddShiftAsync(Start.AddHours(8), Start.AddHours(16), CalendarEventStatusTypeCodes.Active);
        var assignment = new AssignmentEntry
        {
            Event = SchedulingEvent(Start.AddHours(9), Start.AddHours(10), "assignment"),
            AssignmentDefinitionId = 1,
            CategoryId = 1,
            SubCategoryId = 1,
        };
        _db.ShiftAssignmentEntries.Add(
            new ShiftAssignmentEntry
            {
                ShiftEntry = shift,
                AssignmentEntry = assignment,
                Users = [new() { UserId = UserA }],
            }
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var candidates = await SchedulingConflictParticipantProvider.GetParticipantsForPublicationAsync(
            _db,
            [],
            [assignment.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Single(candidates);
        await Assert.ThrowsAsync<CalendarConflictException>(() =>
            _conflicts.EnsureNoUnresolvedConflictsAsync(candidates, TestContext.Current.CancellationToken)
        );
    }

    [Theory]
    [InlineData(7, 8, false)]
    [InlineData(8, 9, true)]
    [InlineData(9, 10, true)]
    [InlineData(10, 11, false)]
    public async Task GetConflictsForCandidatesAsync_WhenTimedLeave_UsesHalfOpenOverlap(
        int startHour,
        int endHour,
        bool expected
    )
    {
        await AddLeaveAsync(Start.AddHours(8), Start.AddHours(10), allDay: false);
        var shift = await AddShiftAsync(Start.AddHours(startHour), Start.AddHours(endHour));
        var candidates = await SchedulingConflictParticipantProvider.GetParticipantsForPublicationAsync(
            _db,
            [shift.Id],
            [],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(
            expected,
            (await _conflicts.GetConflictsForCandidatesAsync(candidates, TestContext.Current.CancellationToken)).Count
                > 0
        );
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(9, 5)]
    [InlineData(9, 9)]
    public async Task GetConflictsAsync_WithLocationScope_RetainsSameAndCrossLocationConflicts(
        int locationId,
        int leaveLocationId
    )
    {
        var leave = await AddLeaveAsync();
        leave.Event!.LocationId = leaveLocationId;
        await AddShiftAsync(Start.AddHours(1), Start.AddHours(2), CalendarEventStatusTypeCodes.Active);
        var result = await _conflicts.GetConflictsAsync(
            Query() with
            {
                LocationId = locationId,
            },
            TestContext.Current.CancellationToken
        );
        Assert.Single(result);
        Assert.Equal("Leave", result.Single().Entry.Title);
        var unrelatedScope = await _conflicts.GetConflictsAsync(
            Query() with
            {
                LocationId = 99,
            },
            TestContext.Current.CancellationToken
        );
        Assert.Empty(unrelatedScope);
    }

    [Fact]
    public async Task GetConflictsAsync_WhenOnlyLeaveIsInScope_IncludesCrossLocationAssignmentAndShift()
    {
        await AddLeaveAsync();
        var shift = await AddShiftAsync(Start.AddHours(1), Start.AddHours(3), CalendarEventStatusTypeCodes.Active);
        var assignmentEvent = SchedulingEvent(Start.AddHours(1), Start.AddHours(2), "assignment");
        assignmentEvent.StatusTypeCode = CalendarEventStatusTypeCodes.Active;
        _db.ShiftAssignmentEntries.Add(
            new ShiftAssignmentEntry
            {
                ShiftEntry = shift,
                AssignmentEntry = new AssignmentEntry
                {
                    Event = assignmentEvent,
                    AssignmentDefinitionId = 1,
                    CategoryId = 1,
                    SubCategoryId = 1,
                },
                Users = [new() { UserId = UserA }],
            }
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var result = await _conflicts.GetConflictsAsync(
            Query() with
            {
                LocationId = 5,
            },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(2, result.Count);
        Assert.All(result, conflict => Assert.Equal("leave", conflict.Entry.SourceModule));
        Assert.Contains(result, conflict => conflict.Overlaps.EventId == assignmentEvent.Id.ToString());
        Assert.Contains(result, conflict => conflict.Overlaps.EventId == shift.EventId.ToString());
        Assert.Empty(
            await _conflicts.GetConflictsAsync(Query() with { LocationId = 99 }, TestContext.Current.CancellationToken)
        );
        Assert.Empty(
            await _conflicts.GetConflictsAsync(
                Query() with
                {
                    LocationId = 5,
                    ResourceIds = [UserB],
                },
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task GetConflictsForCandidatesAsync_WhenOtherSchedulingRecordsAreDraft_DoesNotIntroduceSchedulingConflicts()
    {
        var first = await AddShiftAsync(Start.AddHours(1), Start.AddHours(3));
        await AddShiftAsync(Start.AddHours(2), Start.AddHours(4));
        var candidates = await SchedulingConflictParticipantProvider.GetParticipantsForPublicationAsync(
            _db,
            [first.Id],
            [],
            TestContext.Current.CancellationToken
        );
        Assert.Empty(
            await _conflicts.GetConflictsForCandidatesAsync(candidates, TestContext.Current.CancellationToken)
        );
        await AddLeaveAsync(userId: UserB);
        Assert.Empty(
            await _conflicts.GetConflictsForCandidatesAsync(candidates, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task GetConflictsAsync_WhenLeaveOverlapsLeaveOrHoliday_DoesNotIntroduceNewPairs()
    {
        var first = await AddLeaveAsync();
        await AddLeaveAsync();
        var participant = (
            await _provider.GetParticipantsAsync(Query(), TestContext.Current.CancellationToken)
        ).First();
        Assert.Empty(
            CalendarConflictDetector.Detect([
                participant,
                participant with
                {
                    EventId = "holiday-1",
                    SourceModule = "calendar",
                },
            ])
        );
        Assert.Empty(await _conflicts.GetConflictsAsync(Query(), TestContext.Current.CancellationToken));
        _db.LeaveEntries.Remove(first);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Null(
            await _provider.GetParticipantAsync(
                LeaveConflictParticipantProvider.IdentityFor(first.EventId),
                UserA,
                TestContext.Current.CancellationToken
            )
        );
    }

    private static CalendarConflictQuery Query() => new(Start, Start.AddDays(1));

    private async Task<LeaveEntry> AddLeaveAsync(
        DateTimeOffset? start = null,
        DateTimeOffset? end = null,
        Guid? userId = null,
        bool allDay = true
    )
    {
        var leave = new LeaveEntry
        {
            Event = new Event
            {
                SourceModule = "leave",
                EventTypeCode = "leave",
                StatusTypeCode = CalendarEventStatusTypeCodes.Draft,
                Title = "Restricted leave type",
                Description = "Private description",
                Notes = "Private notes",
                LocationId = 5,
                TimeZoneId = "America/Vancouver",
                StartAtUtc = start ?? Start,
                EndAtUtc = end ?? Start.AddDays(1),
                AllDay = allDay,
            },
            LeaveTypeId = 1,
            Users = [new() { UserId = userId ?? UserA }],
        };
        _db.LeaveEntries.Add(leave);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return leave;
    }

    private async Task<ShiftEntry> AddShiftAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        string status = CalendarEventStatusTypeCodes.Draft
    )
    {
        var shift = new ShiftEntry { Event = SchedulingEvent(start, end, "shift"), Users = [new() { UserId = UserA }] };
        shift.Event.StatusTypeCode = status;
        _db.ShiftEntries.Add(shift);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return shift;
    }

    private static Event SchedulingEvent(DateTimeOffset start, DateTimeOffset end, string type) =>
        new()
        {
            SourceModule = "scheduling",
            EventTypeCode = type,
            StatusTypeCode = CalendarEventStatusTypeCodes.Draft,
            Title = "Schedule",
            StartAtUtc = start,
            EndAtUtc = end,
            LocationId = 9,
            TimeZoneId = "America/Toronto",
        };
}
