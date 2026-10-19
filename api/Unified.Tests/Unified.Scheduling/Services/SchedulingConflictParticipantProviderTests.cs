using Microsoft.EntityFrameworkCore;
using Unified.Authorization;
using Unified.Calendar.Conflicts;
using Unified.Common.Calendar.Conflicts;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling;
using Unified.Scheduling.Services;

namespace Unified.Tests.Scheduling.Services;

public sealed class SchedulingConflictParticipantProviderTests : IAsyncLifetime
{
    private static readonly Guid UserId = new("11111111-1111-1111-1111-111111111111");
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
        shiftEvent.LocationId = 99;
        assignmentEvent.LocationId = 99;
        var link = new ShiftAssignmentEntry
        {
            Id = 30,
            ShiftEntryId = 40,
            ShiftEntry = new ShiftEntry { Id = 40, EventId = shiftEvent.Id, Event = shiftEvent },
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
            new ShiftAssignmentEntryUser { Id = 60, UserId = UserId, ShiftAssignmentEntry = link }
        );
        _db.ShiftEntryUsers.Add(new ShiftEntryUser { Id = 61, UserId = UserId, ShiftEntry = link.ShiftEntry });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await _db.DisposeAsync();

    [Theory]
    [MemberData(nameof(AuthorizedPermissionCases))]
    public async Task GetParticipantsAsync_AuthorizedViewer_ProjectsPermissionAppropriateEvent(
        string[] permissions,
        string expectedEventId,
        string expectedTitle
    )
    {
        var participants = await _provider.GetParticipantsAsync(
            CreateQuery(permissions),
            TestContext.Current.CancellationToken
        );

        var participant = Assert.Single(participants);
        Assert.Equal(expectedEventId, participant.EventId);
        Assert.Equal(expectedTitle, participant.Title);
    }

    public static TheoryData<string[], string, string> AuthorizedPermissionCases =>
        new()
        {
            { [nameof(Permissions.ShiftsView)], "10", "Visible shift" },
            { [nameof(Permissions.AssignmentsView)], "20", "Restricted assignment" },
            {
                [nameof(Permissions.ShiftsView), nameof(Permissions.AssignmentsView)],
                "20",
                "Restricted assignment"
            },
        };

    [Fact]
    public async Task GetParticipantsAsync_WithoutViewPermission_ReturnsNoParticipants()
    {
        var participants = await _provider.GetParticipantsAsync(
            CreateQuery([]),
            TestContext.Current.CancellationToken
        );

        Assert.Empty(participants);
    }

    [Fact]
    public async Task GetParticipantsAsync_WithoutHttpPermissionContext_PreservesAssignmentProjection()
    {
        var participants = await _provider.GetParticipantsAsync(
            CreateQuery(null),
            TestContext.Current.CancellationToken
        );

        Assert.Equal("20", Assert.Single(participants).EventId);
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
            CreateQuery([nameof(Permissions.AssignmentsView)]) with { LocationId = vancouver.Id },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(new[] { "101", "201" }, participants.Select(participant => participant.EventId).Order());
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
            CreateQuery([nameof(Permissions.AssignmentsView)]) with { LocationId = vancouver.Id },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(new[] { "501", "601" }, participants.Select(participant => participant.EventId).Order());
        Assert.True(participants.Single(participant => participant.EventId == "501").IsInQueryScope);
        Assert.False(participants.Single(participant => participant.EventId == "601").IsInQueryScope);
        Assert.Single(CalendarConflictDetector.Detect(participants));
        Assert.DoesNotContain(participants, participant => participant.ResourceId == UnrelatedUserId);
    }

    [Fact]
    public async Task GetParticipantsAsync_ShiftViewerWithLocationScope_DetectsLocationlessAndCrossLocationConflict()
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
            CreateQuery([nameof(Permissions.ShiftsView)]) with { LocationId = vancouver.Id },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(new[] { "1000", "1100" }, participants.Select(participant => participant.EventId).Order());
        Assert.True(participants.Single(participant => participant.EventId == "1000").IsInQueryScope);
        Assert.False(participants.Single(participant => participant.EventId == "1100").IsInQueryScope);
        Assert.Single(CalendarConflictDetector.Detect(participants));
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

        var participant = Assert.Single(participants);
        Assert.Equal("20", participant.EventId);
        Assert.Equal(UserId, participant.ResourceId);
    }

    [Fact]
    public async Task GetParticipantsForShiftEntriesAsync_WhenShiftHasNoAssignmentLinks_ReturnsNoCandidates()
    {
        var draftParticipant = CreateShiftParticipant(1300, MultiLocationUserId, null);
        draftParticipant.ShiftEntry!.Event!.StatusTypeCode = CalendarEventStatusTypeCodes.Draft;
        _db.ShiftEntryUsers.Add(draftParticipant);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var participants = await SchedulingConflictParticipantProvider.GetParticipantsForShiftEntriesAsync(
            _db,
            [1300],
            TestContext.Current.CancellationToken
        );

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
            CreateQuery([nameof(Permissions.AssignmentsView)]) with { ResourceIds = [MultiLocationUserId] },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(2, participants.Count);
        Assert.All(participants, participant => Assert.Null(participant.TimeZoneId));
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
            CreateQuery([nameof(Permissions.AssignmentsView)]) with { ResourceIds = [MultiLocationUserId] },
            TestContext.Current.CancellationToken
        );

        Assert.Equal("America/Vancouver", Assert.Single(participants).TimeZoneId);
    }

    private static CalendarConflictQuery CreateQuery(IEnumerable<string>? permissions) =>
        new(
            new DateTimeOffset(2026, 8, 24, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 25, 0, 0, 0, TimeSpan.Zero),
            ViewerPermissions: permissions?.ToHashSet(StringComparer.Ordinal)
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
                ShiftEntry = new ShiftEntry { Id = id, EventId = shiftEvent.Id, Event = shiftEvent },
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
            ShiftEntry = new ShiftEntry { Id = eventId, EventId = shiftEvent.Id, Event = shiftEvent },
        };
    }
}
