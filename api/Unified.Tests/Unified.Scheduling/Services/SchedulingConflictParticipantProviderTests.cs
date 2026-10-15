using Microsoft.EntityFrameworkCore;
using Unified.Authorization;
using Unified.Common.Calendar.Conflicts;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling;
using Unified.Scheduling.Services;

namespace Unified.Tests.Scheduling.Services;

public sealed class SchedulingConflictParticipantProviderTests : IAsyncLifetime
{
    private static readonly Guid UserId = new("11111111-1111-1111-1111-111111111111");
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
}