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
    private static readonly DateTimeOffset AssignmentStart = new(2026, 8, 24, 16, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset AssignmentEnd = new(2026, 8, 24, 18, 45, 0, TimeSpan.Zero);
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
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await _db.DisposeAsync();

    [Theory]
    [MemberData(nameof(PermissionCases))]
    public async Task GetParticipantsAsync_ViewerPermissions_ReturnsOnlyAuthorizedAssignments(
        string[]? permissions,
        bool expectedParticipant
    )
    {
        var participants = await _provider.GetParticipantsAsync(
            CreateQuery(permissions),
            TestContext.Current.CancellationToken
        );

        if (!expectedParticipant)
        {
            Assert.Empty(participants);
            return;
        }
        var participant = Assert.Single(participants);
        Assert.Equal("20", participant.EventId);
        Assert.Equal("Restricted assignment", participant.Title);
        Assert.Equal(AssignmentStart, participant.Start);
        Assert.Equal(AssignmentEnd, participant.End);
        Assert.Equal(UserId, participant.ResourceId);
        Assert.Equal(50, participant.SourceEntityId);
        Assert.Equal("scheduling", participant.SourceModule);
        Assert.Equal("America/Vancouver", participant.TimeZoneId);
        Assert.Equal(
            await _provider.GetParticipantsAsync(CreateQuery(null), TestContext.Current.CancellationToken),
            participants
        );
    }

    public static TheoryData<string[]?, bool> PermissionCases =>
        new()
        {
            { [nameof(Permissions.AssignmentsView)], true },
            { [nameof(Permissions.ShiftsView), nameof(Permissions.AssignmentsView)], true },
            { [nameof(Permissions.ShiftsView)], false },
            { [], false },
            { null, true },
        };

    [Theory]
    [InlineData(false, "draft", false, 0)]
    [InlineData(true, "draft", false, 0)]
    [InlineData(false, "draft", true, 1)]
    [InlineData(true, "draft", true, 1)]
    [InlineData(false, "cancelled", true, 0)]
    [InlineData(true, "cancelled", true, 0)]
    public async Task GetParticipantsAsync_AuthorizedViewer_PreservesStatusFilters(
        bool updateShift,
        string status,
        bool includeDraft,
        int expectedCount
    )
    {
        var eventEntity = await _db.Events.FindAsync([updateShift ? 10 : 20], TestContext.Current.CancellationToken);
        eventEntity!.StatusTypeCode = status;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var participants = await _provider.GetParticipantsAsync(
            CreateQuery([nameof(Permissions.AssignmentsView)]) with
            {
                IncludeDraftParticipants = includeDraft,
            },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(expectedCount, participants.Count);
    }

    [Fact]
    public async Task GetParticipantsAsync_AuthorizedViewer_PreservesResourceAndHalfOpenRangeFilters()
    {
        var query = CreateQuery([nameof(Permissions.AssignmentsView)]);
        Assert.Single(
            await _provider.GetParticipantsAsync(
                query with
                {
                    ResourceIds = [UserId],
                },
                TestContext.Current.CancellationToken
            )
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
            await _provider.GetParticipantsAsync(
                query with
                {
                    EndAtUtc = AssignmentStart,
                },
                TestContext.Current.CancellationToken
            )
        );
        Assert.Empty(
            await _provider.GetParticipantsAsync(
                query with
                {
                    StartAtUtc = AssignmentEnd,
                },
                TestContext.Current.CancellationToken
            )
        );
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
