using Microsoft.EntityFrameworkCore;
using Unified.Authorization;
using Unified.Calendar.Conflicts;
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
        _conflicts = new CalendarConflictService(
            [_provider, new SchedulingConflictParticipantProvider(_db)],
            _db,
            CalendarConflictTestRules.Scheduling
        );
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task GetParticipantsAsync_WhenDraftExcluded_StillIncludesRecordedLeaveWithoutViewPermissions()
    {
        var leave = await AddLeaveAsync();
        var participants = await _provider.GetParticipantsAsync(
            Query() with
            {
                IncludeDraftParticipants = false,
                ViewerPermissions = new HashSet<string>(),
                LocationId = 9,
            },
            TestContext.Current.CancellationToken
        );
        var participant = Assert.Single(participants);
        Assert.Equal(LeaveConflictParticipantProvider.IdentityFor(leave.EventId), participant.Identity);
        Assert.Equal(leave.Id, participant.SourceEntityId);
        Assert.Equal(UserA, participant.ResourceId);
        Assert.Equal("Leave", participant.Title);
        Assert.Equal("leave", participant.EventTypeCode);
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

    [Fact]
    public async Task GetConflictsAsync_WhenLeaveIsAtAnotherLocationAndTimeZone_RetainsOnlyScopedConflicts()
    {
        await AddLeaveAsync(); // Location 5, America/Vancouver.
        await AddShiftAsync(Start.AddHours(1), Start.AddHours(2), CalendarEventStatusTypeCodes.Active);
        var result = await _conflicts.GetConflictsAsync(
            Query() with
            {
                LocationId = 9,
                ViewerPermissions = new HashSet<string> { nameof(Permissions.ShiftsView) },
            },
            TestContext.Current.CancellationToken
        );
        Assert.Single(result);
        Assert.Equal("Leave", result.Single().Entry.Title);
        var unrelatedScope = await _conflicts.GetConflictsAsync(
            Query() with
            {
                LocationId = 99,
                ViewerPermissions = new HashSet<string> { nameof(Permissions.ShiftsView) },
            },
            TestContext.Current.CancellationToken
        );
        Assert.Empty(unrelatedScope);
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
        Assert.Single(
            CalendarConflictDetector.Detect([
                participant,
                participant with
                {
                    EventId = "holiday-1",
                    SourceModule = "calendar",
                    EventTypeCode = CalendarEventTypeCodes.Holiday,
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

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    public async Task DirectionalRules_CrossLocationLeave_ReportsAndBlocksOnlyConfiguredSide(
        bool reportOnShift,
        bool reportOnLeave,
        bool expectedScopedConflict
    )
    {
        var leave = await AddLeaveAsync(); // Vancouver, outside the requested location.
        var shift = await AddShiftAsync(Start.AddHours(1), Start.AddHours(2), CalendarEventStatusTypeCodes.Active);
        var scheduling = new SchedulingConflictParticipantProvider(_db);
        var service = new CalendarConflictService(
            [_provider, scheduling],
            _db,
            CalendarConflictTestRules.Create(
                ("shift", reportOnShift ? ["leave"] : []),
                ("leave", reportOnLeave ? ["shift"] : [])
            )
        );

        var scoped = await service.GetConflictsAsync(
            Query() with
            {
                LocationId = 9,
            },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(expectedScopedConflict ? 1 : 0, scoped.Count);
        var shiftParticipant = await scheduling.GetParticipantAsync(
            new("scheduling", shift.EventId.ToString()),
            UserA,
            TestContext.Current.CancellationToken
        );
        var leaveParticipant = await _provider.GetParticipantAsync(
            LeaveConflictParticipantProvider.IdentityFor(leave.EventId),
            UserA,
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(shiftParticipant);
        Assert.NotNull(leaveParticipant);
        Assert.Equal(
            reportOnShift ? 1 : 0,
            (
                await service.GetConflictsForCandidatesAsync([shiftParticipant], TestContext.Current.CancellationToken)
            ).Count
        );
        Assert.Equal(
            reportOnLeave ? 1 : 0,
            (
                await service.GetConflictsForCandidatesAsync([leaveParticipant], TestContext.Current.CancellationToken)
            ).Count
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
