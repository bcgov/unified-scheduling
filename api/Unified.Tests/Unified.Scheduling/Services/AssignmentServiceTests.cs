using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Unified.Calendar.Conflicts;
using Unified.Calendar.Models;
using Unified.Calendar.Options;
using Unified.Calendar.Services;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Lookup;
using Unified.Db.Models.Scheduling;
using Unified.Db.Models.UserManagement;
using Unified.Scheduling;
using Unified.Scheduling.Models;
using Unified.Scheduling.Services;
using Unified.Tests.TestHelpers;

namespace Unified.Tests.Scheduling.Services;

public sealed class AssignmentServiceTests : IAsyncLifetime
{
    private static readonly Guid UserA = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserB = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CancelledByUser = new("44444444-4444-4444-4444-444444444444");

    private SqliteConnection _connection = null!;
    private UnifiedDbContext _dbContext = null!;
    private AssignmentService _assignmentService = null!;
    private ShiftAssignmentService _shiftAssignmentService = null!;
    private ShiftService _shiftService = null!;
    private CalendarConflictService _calendarConflictService = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.CreateFunction("now", () => DateTimeOffset.UtcNow.ToString("O"));
        await _connection.OpenAsync(TestContext.Current.CancellationToken);

        var options = new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite(_connection).Options;
        _dbContext = new SqliteTestUnifiedDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await SeedBaseDataAsync();

        var calendarDateTimeService = CreateCalendarDateTimeService();
        var recurrenceExpander = new IcalNetRecurrenceExpander(calendarDateTimeService);
        var recurrenceRuleValidator = new IcalNetRecurrenceRuleValidator(recurrenceExpander, calendarDateTimeService);
        var materializationService = new EventSeriesMaterializationService(
            _dbContext,
            recurrenceRuleValidator,
            recurrenceExpander
        );

        _shiftAssignmentService = new ShiftAssignmentService(
            NullLogger<ShiftAssignmentService>.Instance,
            _dbContext,
            calendarDateTimeService
        );

        _assignmentService = new AssignmentService(
            NullLogger<AssignmentService>.Instance,
            _dbContext,
            materializationService,
            new AssignmentSeriesMaterializationHandler(_dbContext),
            _shiftAssignmentService,
            new CalendarLifecycleService(),
            new AllowAllCalendarConflictService()
        );
        var conflictParticipantProvider = new SchedulingConflictParticipantProvider(_dbContext);
        _calendarConflictService = new CalendarConflictService(
            new CalendarConflictDetector(),
            [conflictParticipantProvider],
            _dbContext
        );
        _shiftService = new ShiftService(
            NullLogger<ShiftService>.Instance,
            _dbContext,
            materializationService,
            new ShiftSeriesMaterializationHandler(_dbContext),
            _shiftAssignmentService,
            calendarDateTimeService,
            new CalendarLifecycleService(),
            _calendarConflictService,
            conflictParticipantProvider
        );
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task CreateAssignmentSeriesAsync_WhenDefinitionProvided_UsesDefinitionDefaults()
    {
        var request = CreateAssignmentSeriesRequest(
            capacity: null,
            startAtUtc: new DateTimeOffset(2026, 6, 1, 15, 0, 0, TimeSpan.Zero),
            endAtUtc: null
        );

        var result = await _assignmentService.CreateAssignmentSeriesAsync(
            request,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(1, result.AssignmentDefinitionId);
        Assert.Equal(10, result.AssignmentCategoryTypeId);
        Assert.Equal(20, result.AssignmentSubCategoryTypeId);
        Assert.Equal(2, result.Capacity);
        Assert.Equal(new DateTimeOffset(2026, 6, 1, 15, 0, 0, TimeSpan.Zero), result.StartAtUtc);
        Assert.Equal(new DateTimeOffset(2026, 6, 1, 22, 0, 0, TimeSpan.Zero), result.EndAtUtc);
        Assert.Equal("blue", result.Color);
        Assert.All(result.Entries, entry => Assert.Equal(2, entry.Capacity));
        Assert.All(result.Entries, entry => Assert.Equal("blue", entry.Color));
    }

    [Fact]
    public async Task CreateAssignmentSeriesAsync_WhenExplicitEndAtMidnightUtc_PreservesProvidedEndTime()
    {
        var result = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(
                recurrenceRule: "RRULE:FREQ=WEEKLY;INTERVAL=1;BYDAY=MO,WE,TU,TH,FR;COUNT=5",
                startAtUtc: new DateTimeOffset(2026, 7, 13, 16, 0, 0, TimeSpan.Zero),
                endAtUtc: new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero)
            ),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero), result.EndAtUtc);
        Assert.All(result.Entries, entry => Assert.Equal(TimeSpan.FromHours(8), entry.EndAtUtc - entry.StartAtUtc));
        var friday = Assert.Single(
            result.Entries,
            entry => entry.StartAtUtc == new DateTimeOffset(2026, 7, 17, 16, 0, 0, TimeSpan.Zero)
        );
        Assert.Equal(new DateTimeOffset(2026, 7, 18, 0, 0, 0, TimeSpan.Zero), friday.EndAtUtc);
    }

    [Fact]
    public async Task CreateAssignmentEntryAsync_WhenValid_CreatesActiveAssignmentEntry()
    {
        var result = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(),
            TestContext.Current.CancellationToken
        );

        var entry = await _dbContext
            .AssignmentEntries.Include(x => x.Event)
            .SingleAsync(x => x.Id == result.Id, TestContext.Current.CancellationToken);
        Assert.Equal(CalendarEventStatusTypeCodes.Active, entry.Event!.StatusTypeCode);
        Assert.Equal(SchedulingConstants.AssignmentEventTypeCode, entry.Event.EventTypeCode);
        Assert.Equal(2, entry.Capacity);
        Assert.Equal("Assignment", result.Title);
        Assert.Equal("blue", result.Color);
        Assert.Equal(entry.Event.StartAtUtc, result.StartAtUtc);
        Assert.Equal(entry.Event.EndAtUtc, result.EndAtUtc);
        Assert.Equal(entry.Event.LocationId, result.LocationId);
        Assert.Equal(CalendarEventStatusTypeCodes.Active, result.StatusTypeCode);
    }

    [Fact]
    public async Task CreateAssignmentEntryAsync_WhenAssignedUserAlreadyOccupiesOverlappingAssignment_RejectsConflict()
    {
        var conflictAwareService = CreateConflictAwareAssignmentService();
        var firstShift = await AddShiftEntryAsync();
        var secondShift = await AddShiftEntryAsync();

        await conflictAwareService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(shiftEntryIds: [firstShift.Id], assignedUserIds: [UserA]),
            TestContext.Current.CancellationToken
        );
        var publishedParticipants = await new SchedulingConflictParticipantProvider(_dbContext).GetParticipantsAsync(
            new CalendarConflictQuery(
                new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero)
            ),
            TestContext.Current.CancellationToken
        );
        Assert.Single(publishedParticipants);

        var exception = await Assert.ThrowsAsync<CalendarConflictException>(() =>
            conflictAwareService.CreateAssignmentEntryAsync(
                CreateAssignmentEntryRequest(
                    title: "Overlapping assignment",
                    shiftEntryIds: [secondShift.Id],
                    assignedUserIds: [UserA]
                ),
                TestContext.Current.CancellationToken
            )
        );

        Assert.Single(exception.Conflicts);
        Assert.Equal("This operation would cause a conflict with an existing event", exception.Message);
        Assert.Equal(UserA, exception.Conflicts.Single().ResourceId);
        Assert.Single(await _dbContext.AssignmentEntries.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateAssignmentEntryAsync_WhenOverlappingAssignmentsAreLinkedToDraftShifts_AllowsBoth()
    {
        var conflictAwareService = CreateConflictAwareAssignmentService();
        var firstShift = await AddShiftEntryAsync(statusTypeCode: CalendarEventStatusTypeCodes.Draft);
        var secondShift = await AddShiftEntryAsync(statusTypeCode: CalendarEventStatusTypeCodes.Draft);

        await conflictAwareService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(shiftEntryIds: [firstShift.Id], assignedUserIds: [UserA]),
            TestContext.Current.CancellationToken
        );
        await conflictAwareService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(
                title: "Overlapping draft assignment",
                shiftEntryIds: [secondShift.Id],
                assignedUserIds: [UserA]
            ),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(2, await _dbContext.AssignmentEntries.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PublishShiftEntryAsync_WhenDraftShiftAssignmentsConflict_RejectsPublishAndKeepsShiftDraft()
    {
        var conflictAwareService = CreateConflictAwareAssignmentService();
        var firstShift = await AddShiftEntryAsync(statusTypeCode: CalendarEventStatusTypeCodes.Draft);
        var secondShift = await AddShiftEntryAsync(statusTypeCode: CalendarEventStatusTypeCodes.Draft);
        var firstAssignment = await conflictAwareService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(shiftEntryIds: [firstShift.Id], assignedUserIds: [UserA]),
            TestContext.Current.CancellationToken
        );
        var secondAssignment = await conflictAwareService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(
                title: "Overlapping draft assignment",
                shiftEntryIds: [secondShift.Id],
                assignedUserIds: [UserA]
            ),
            TestContext.Current.CancellationToken
        );

        var exception = await Assert.ThrowsAsync<CalendarConflictException>(() =>
            _shiftService.PublishShiftEntryAsync(firstShift.Id, TestContext.Current.CancellationToken)
        );

        Assert.Equal("This operation would cause a conflict with an existing event", exception.Message);
        _dbContext.ChangeTracker.Clear();
        var reloaded = await _dbContext
            .ShiftEntries.Include(entry => entry.Event)
            .SingleAsync(entry => entry.Id == firstShift.Id, TestContext.Current.CancellationToken);
        Assert.Equal(CalendarEventStatusTypeCodes.Draft, reloaded.Event!.StatusTypeCode);

        var conflict = Assert.Single(
            await _calendarConflictService.GetConflictsAsync(
                new CalendarConflictQuery(firstAssignment.StartAtUtc!.Value, firstAssignment.EndAtUtc!.Value),
                TestContext.Current.CancellationToken
            )
        );
        Assert.Equal(
            [firstAssignment.EventId, secondAssignment.EventId],
            new[] { conflict.Entry.EventId!.Value, conflict.Overlaps.EventId!.Value }.Order().ToArray()
        );
    }

    [Fact]
    public async Task PublishShiftEntryAsync_WhenDraftConflictHasExplicitOverride_AllowsPublish()
    {
        var conflictAwareService = CreateConflictAwareAssignmentService();
        var firstShift = await AddShiftEntryAsync(statusTypeCode: CalendarEventStatusTypeCodes.Draft);
        var secondShift = await AddShiftEntryAsync(statusTypeCode: CalendarEventStatusTypeCodes.Draft);
        var firstAssignment = await conflictAwareService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(shiftEntryIds: [firstShift.Id], assignedUserIds: [UserA]),
            TestContext.Current.CancellationToken
        );
        var secondAssignment = await conflictAwareService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(
                title: "Overridden draft assignment",
                shiftEntryIds: [secondShift.Id],
                assignedUserIds: [UserA]
            ),
            TestContext.Current.CancellationToken
        );
        await _calendarConflictService.CreateOverrideAsync(
            new CalendarConflictOverrideRequest
            {
                FirstEventId = firstAssignment.EventId,
                SecondEventId = secondAssignment.EventId,
                Note = "Approved before publishing",
            },
            null,
            TestContext.Current.CancellationToken
        );

        var result = await _shiftService.PublishShiftEntryAsync(firstShift.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(CalendarEventStatusTypeCodes.Active, result.StatusTypeCode);
    }

    [Fact]
    public async Task UpdateAssignmentEntryAsync_WhenOverlapIsOnDraftShifts_AllowsUpdate()
    {
        var conflictAwareService = CreateConflictAwareAssignmentService();
        var firstShift = await AddShiftEntryAsync(statusTypeCode: CalendarEventStatusTypeCodes.Draft);
        var secondShift = await AddShiftEntryAsync(statusTypeCode: CalendarEventStatusTypeCodes.Draft);
        await conflictAwareService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(
                startAtUtc: new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
                endAtUtc: new DateTimeOffset(2026, 6, 1, 19, 0, 0, TimeSpan.Zero),
                shiftEntryIds: [firstShift.Id],
                assignedUserIds: [UserA]
            ),
            TestContext.Current.CancellationToken
        );
        var secondAssignment = await conflictAwareService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(
                startAtUtc: new DateTimeOffset(2026, 6, 1, 20, 0, 0, TimeSpan.Zero),
                endAtUtc: new DateTimeOffset(2026, 6, 1, 22, 0, 0, TimeSpan.Zero),
                shiftEntryIds: [secondShift.Id],
                assignedUserIds: [UserA]
            ),
            TestContext.Current.CancellationToken
        );

        var result = await conflictAwareService.UpdateAssignmentEntryAsync(
            secondAssignment.Id,
            CreateAssignmentEntryRequest(
                startAtUtc: new DateTimeOffset(2026, 6, 1, 18, 0, 0, TimeSpan.Zero),
                endAtUtc: new DateTimeOffset(2026, 6, 1, 21, 0, 0, TimeSpan.Zero),
                shiftEntryIds: [secondShift.Id],
                assignedUserIds: [UserA]
            ),
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(result);
        Assert.Equal(new DateTimeOffset(2026, 6, 1, 18, 0, 0, TimeSpan.Zero), result.StartAtUtc);
    }

    [Fact]
    public async Task UpdateAssignmentEntryAsync_WhenOverriddenPairNoLongerConflicts_DeactivatesOverride()
    {
        var conflictAwareService = CreateConflictAwareAssignmentService();
        var firstShift = await AddShiftEntryAsync(statusTypeCode: CalendarEventStatusTypeCodes.Draft);
        var secondShift = await AddShiftEntryAsync(statusTypeCode: CalendarEventStatusTypeCodes.Draft);
        var firstAssignment = await conflictAwareService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(
                startAtUtc: new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
                endAtUtc: new DateTimeOffset(2026, 6, 1, 19, 0, 0, TimeSpan.Zero),
                shiftEntryIds: [firstShift.Id],
                assignedUserIds: [UserA]
            ),
            TestContext.Current.CancellationToken
        );
        var secondAssignment = await conflictAwareService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(
                startAtUtc: new DateTimeOffset(2026, 6, 1, 18, 0, 0, TimeSpan.Zero),
                endAtUtc: new DateTimeOffset(2026, 6, 1, 21, 0, 0, TimeSpan.Zero),
                shiftEntryIds: [secondShift.Id],
                assignedUserIds: [UserA]
            ),
            TestContext.Current.CancellationToken
        );
        await _calendarConflictService.CreateOverrideAsync(
            new CalendarConflictOverrideRequest
            {
                FirstEventId = firstAssignment.EventId,
                SecondEventId = secondAssignment.EventId,
                Note = "Approved overlap",
            },
            null,
            TestContext.Current.CancellationToken
        );

        await conflictAwareService.UpdateAssignmentEntryAsync(
            secondAssignment.Id,
            CreateAssignmentEntryRequest(
                startAtUtc: new DateTimeOffset(2026, 6, 1, 19, 0, 0, TimeSpan.Zero),
                endAtUtc: new DateTimeOffset(2026, 6, 1, 22, 0, 0, TimeSpan.Zero),
                shiftEntryIds: [secondShift.Id],
                assignedUserIds: [UserA]
            ),
            TestContext.Current.CancellationToken
        );

        var persistedOverride = await _dbContext.CalendarConflictOverrides.SingleAsync(
            TestContext.Current.CancellationToken
        );
        Assert.False(persistedOverride.IsActive);
        Assert.NotNull(persistedOverride.InvalidatedOn);
    }

    [Fact]
    public async Task UpdateAssignmentEntryAsync_WhenOverlapIsOnPublishedShifts_RejectsUpdate()
    {
        var conflictAwareService = CreateConflictAwareAssignmentService();
        var firstShift = await AddShiftEntryAsync();
        var secondShift = await AddShiftEntryAsync();
        await conflictAwareService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(
                startAtUtc: new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
                endAtUtc: new DateTimeOffset(2026, 6, 1, 19, 0, 0, TimeSpan.Zero),
                shiftEntryIds: [firstShift.Id],
                assignedUserIds: [UserA]
            ),
            TestContext.Current.CancellationToken
        );
        var secondAssignment = await conflictAwareService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(
                startAtUtc: new DateTimeOffset(2026, 6, 1, 20, 0, 0, TimeSpan.Zero),
                endAtUtc: new DateTimeOffset(2026, 6, 1, 22, 0, 0, TimeSpan.Zero),
                shiftEntryIds: [secondShift.Id],
                assignedUserIds: [UserA]
            ),
            TestContext.Current.CancellationToken
        );

        var exception = await Assert.ThrowsAsync<CalendarConflictException>(() =>
            conflictAwareService.UpdateAssignmentEntryAsync(
                secondAssignment.Id,
                CreateAssignmentEntryRequest(
                    startAtUtc: new DateTimeOffset(2026, 6, 1, 18, 0, 0, TimeSpan.Zero),
                    endAtUtc: new DateTimeOffset(2026, 6, 1, 21, 0, 0, TimeSpan.Zero),
                    shiftEntryIds: [secondShift.Id],
                    assignedUserIds: [UserA]
                ),
                TestContext.Current.CancellationToken
            )
        );

        Assert.Equal("This operation would cause a conflict with an existing event", exception.Message);
    }

    [Fact]
    public async Task CreateAssignmentEntryAsync_WhenFutureDefinitionIsEffectiveOnAssignmentDate_CreatesAssignment()
    {
        var result = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(
                assignmentDefinitionId: 3,
                startAtUtc: new DateTimeOffset(2026, 7, 22, 0, 0, 0, TimeSpan.Zero)
            ),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(3, result.AssignmentDefinitionId);
        Assert.Equal(new DateTimeOffset(2026, 7, 22, 0, 0, 0, TimeSpan.Zero), result.StartAtUtc);
    }

    [Fact]
    public async Task CreateAssignmentEntryAsync_WhenFutureDefinitionIsNotEffectiveOnAssignmentDate_ThrowsInactiveDefinition()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assignmentService.CreateAssignmentEntryAsync(
                CreateAssignmentEntryRequest(
                    assignmentDefinitionId: 3,
                    startAtUtc: new DateTimeOffset(2026, 7, 21, 16, 0, 0, TimeSpan.Zero)
                ),
                TestContext.Current.CancellationToken
            )
        );

        Assert.Equal("Assignment definition is not active.", exception.Message);
    }

    [Fact]
    public async Task CreateAssignmentEntryAsync_WhenExplicitEndAtMidnightUtc_PreservesProvidedEndTime()
    {
        var result = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(
                startAtUtc: new DateTimeOffset(2026, 7, 13, 16, 0, 0, TimeSpan.Zero),
                endAtUtc: new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero)
            ),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(new DateTimeOffset(2026, 7, 13, 16, 0, 0, TimeSpan.Zero), result.StartAtUtc);
        Assert.Equal(new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero), result.EndAtUtc);
    }

    [Fact]
    public async Task CreateAssignmentEntryAsync_WhenShiftEntryIdsProvided_CreatesLinksInOneTransaction()
    {
        var firstShift = await AddShiftEntryAsync(userIds: [UserA, UserB]);
        var secondShift = await AddShiftEntryAsync(
            startAtUtc: new DateTimeOffset(2026, 6, 2, 16, 0, 0, TimeSpan.Zero),
            userIds: [UserA, UserB]
        );

        var result = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(shiftEntryIds: [firstShift.Id, secondShift.Id], assignedUserIds: [UserA]),
            TestContext.Current.CancellationToken
        );

        Assert.Equal([firstShift.Id, secondShift.Id], result.LinkedShiftEntryIds.Order().ToArray());
        Assert.Equal([UserA], result.AssignedUserIds);
        Assert.Equal(2, await _dbContext.ShiftAssignmentEntries.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateAssignmentEntryAsync_WhenSyncingOneAssignment_DoesNotRemoveOtherAssignmentsLinkedToSameShift()
    {
        var shift = await AddShiftEntryAsync(userIds: [UserA]);
        var assignmentA = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(title: "Assignment A"),
            TestContext.Current.CancellationToken
        );
        var assignmentB = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(title: "Assignment B"),
            TestContext.Current.CancellationToken
        );
        await _shiftAssignmentService.LinkShiftEntryAsync(
            new ShiftAssignmentEntryRequest
            {
                ShiftEntryId = shift.Id,
                AssignmentEntryId = assignmentA.Id,
                UserIds = [UserA],
            },
            TestContext.Current.CancellationToken
        );
        await _shiftAssignmentService.LinkShiftEntryAsync(
            new ShiftAssignmentEntryRequest
            {
                ShiftEntryId = shift.Id,
                AssignmentEntryId = assignmentB.Id,
                UserIds = [UserA],
            },
            TestContext.Current.CancellationToken
        );

        await _assignmentService.UpdateAssignmentEntryAsync(
            assignmentB.Id,
            CreateAssignmentEntryRequest(
                title: "Assignment B updated",
                shiftEntryIds: [shift.Id],
                assignedUserIds: [UserA]
            ),
            TestContext.Current.CancellationToken
        );

        var links = await _dbContext
            .ShiftAssignmentEntries.Where(link => link.ShiftEntryId == shift.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, links.Count);
        Assert.Single(links, link => link.AssignmentEntryId == assignmentA.Id);
        Assert.Single(links, link => link.AssignmentEntryId == assignmentB.Id);
    }

    [Fact]
    public async Task UpdateAssignmentEntryAsync_WhenShiftEntryIdsOmitExistingShift_RemovesOnlyThatAssignmentLink()
    {
        var shift1 = await AddShiftEntryAsync(userIds: [UserA]);
        var shift2 = await AddShiftEntryAsync(
            startAtUtc: new DateTimeOffset(2026, 6, 2, 16, 0, 0, TimeSpan.Zero),
            userIds: [UserA]
        );
        var assignmentA = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(title: "Assignment A"),
            TestContext.Current.CancellationToken
        );
        var assignmentB = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(title: "Assignment B"),
            TestContext.Current.CancellationToken
        );
        await _shiftAssignmentService.LinkShiftEntryAsync(
            new ShiftAssignmentEntryRequest
            {
                ShiftEntryId = shift1.Id,
                AssignmentEntryId = assignmentA.Id,
                UserIds = [UserA],
            },
            TestContext.Current.CancellationToken
        );
        await _shiftAssignmentService.LinkShiftEntryAsync(
            new ShiftAssignmentEntryRequest
            {
                ShiftEntryId = shift1.Id,
                AssignmentEntryId = assignmentB.Id,
                UserIds = [UserA],
            },
            TestContext.Current.CancellationToken
        );
        await _shiftAssignmentService.LinkShiftEntryAsync(
            new ShiftAssignmentEntryRequest
            {
                ShiftEntryId = shift2.Id,
                AssignmentEntryId = assignmentB.Id,
                UserIds = [UserA],
            },
            TestContext.Current.CancellationToken
        );

        await _assignmentService.UpdateAssignmentEntryAsync(
            assignmentB.Id,
            CreateAssignmentEntryRequest(
                title: "Assignment B updated",
                startAtUtc: shift2.Event!.StartAtUtc,
                endAtUtc: shift2.Event.EndAtUtc,
                shiftEntryIds: [shift2.Id],
                assignedUserIds: [UserA]
            ),
            TestContext.Current.CancellationToken
        );

        Assert.False(
            await _dbContext.ShiftAssignmentEntries.AnyAsync(
                link => link.ShiftEntryId == shift1.Id && link.AssignmentEntryId == assignmentB.Id,
                TestContext.Current.CancellationToken
            )
        );
        Assert.True(
            await _dbContext.ShiftAssignmentEntries.AnyAsync(
                link => link.ShiftEntryId == shift2.Id && link.AssignmentEntryId == assignmentB.Id,
                TestContext.Current.CancellationToken
            )
        );
        Assert.True(
            await _dbContext.ShiftAssignmentEntries.AnyAsync(
                link => link.ShiftEntryId == shift1.Id && link.AssignmentEntryId == assignmentA.Id,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task CreateAssignmentSeriesAsync_WhenValid_MaterializesActiveEntriesWithCopiedFields()
    {
        var result = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(2, result.AssignmentEntryIds.Count);
        Assert.Equal(2, result.Entries.Count);
        Assert.All(result.Entries, entry => Assert.Equal(CalendarEventStatusTypeCodes.Active, entry.StatusTypeCode));
        var entries = await _dbContext
            .AssignmentEntries.Include(x => x.Event)
            .Where(x => x.AssignmentSeriesId == result.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.All(
            entries,
            entry =>
            {
                Assert.Equal(1, entry.AssignmentDefinitionId);
                Assert.Equal(2, entry.Capacity);
                Assert.Equal(CalendarEventStatusTypeCodes.Active, entry.Event!.StatusTypeCode);
            }
        );
    }

    [Fact]
    public async Task CreateAssignmentSeriesAsync_WhenShiftSeriesIdsProvided_LinksMatchingEntriesInTransaction()
    {
        var firstShiftSeries = await AddShiftSeriesWithEntriesAsync([
            new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 2, 16, 0, 0, TimeSpan.Zero),
        ]);
        var secondShiftSeries = await AddShiftSeriesWithEntriesAsync([
            new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 2, 16, 0, 0, TimeSpan.Zero),
        ]);

        var result = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(
                shiftSeriesIds: [firstShiftSeries.Id, secondShiftSeries.Id],
                assignedUserIds: [UserA]
            ),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(2, result.Entries.Count);
        Assert.Equal(4, await _dbContext.ShiftAssignmentEntries.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateAssignmentSeriesAsync_WhenShiftSeriesLinksProvided_LinksMatchingEntriesWithSelectedUsers()
    {
        var shiftSeries = await AddShiftSeriesWithEntriesAsync([
            new DateTimeOffset(2026, 7, 7, 16, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 8, 16, 0, 0, TimeSpan.Zero),
        ]);

        var result = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(
                recurrenceRule: "RRULE:FREQ=DAILY;COUNT=2",
                startAtUtc: new DateTimeOffset(2026, 7, 7, 16, 0, 0, TimeSpan.Zero),
                shiftSeriesLinks:
                [
                    new ShiftSeriesLinkRequest { ShiftSeriesId = shiftSeries.Id, AssignedUserIds = [UserA] },
                ]
            ),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(2, result.Entries.Count);
        var responseLink = Assert.Single(result.ShiftSeriesLinks);
        Assert.Equal(shiftSeries.Id, responseLink.ShiftSeriesId);
        Assert.Equal([UserA], responseLink.AssignedUserIds);

        var links = await _dbContext
            .ShiftAssignmentEntries.Include(link => link.Users)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, links.Count);
        Assert.All(links, link => Assert.Equal([UserA], link.Users.Select(user => user.UserId)));
    }

    [Fact]
    public async Task CreateAssignmentSeriesAsync_WhenWeeklyShiftSeriesLinkProvided_LinksMatchingEntries()
    {
        var shiftSeries = await AddShiftSeriesWithEntriesAsync([
            new DateTimeOffset(2026, 7, 7, 16, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 8, 16, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 9, 16, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 12, 16, 0, 0, TimeSpan.Zero),
        ]);

        var result = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(
                recurrenceRule: "RRULE:FREQ=WEEKLY;INTERVAL=1;BYDAY=TU,WE,TH,SU;COUNT=4",
                startAtUtc: new DateTimeOffset(2026, 7, 7, 16, 0, 0, TimeSpan.Zero),
                endAtUtc: new DateTimeOffset(2026, 7, 8, 0, 0, 0, TimeSpan.Zero),
                shiftSeriesLinks:
                [
                    new ShiftSeriesLinkRequest { ShiftSeriesId = shiftSeries.Id, AssignedUserIds = [UserA] },
                ]
            ),
            TestContext.Current.CancellationToken
        );

        Assert.NotEmpty(result.Entries);
        Assert.True(await _dbContext.ShiftAssignmentEntries.AnyAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateAssignmentSeriesAsync_WhenShiftSeriesLinkHasNoOverlappingEntries_Throws()
    {
        var shiftSeries = await AddShiftSeriesWithEntriesAsync([
            new DateTimeOffset(2026, 8, 1, 16, 0, 0, TimeSpan.Zero),
        ]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assignmentService.CreateAssignmentSeriesAsync(
                CreateAssignmentSeriesRequest(
                    recurrenceRule: "RRULE:FREQ=DAILY;COUNT=1",
                    startAtUtc: new DateTimeOffset(2026, 7, 7, 16, 0, 0, TimeSpan.Zero),
                    shiftSeriesLinks:
                    [
                        new ShiftSeriesLinkRequest { ShiftSeriesId = shiftSeries.Id, AssignedUserIds = [UserA] },
                    ]
                ),
                TestContext.Current.CancellationToken
            )
        );

        Assert.Contains("did not overlap any assignment entries", exception.Message);
        Assert.Equal(0, await _dbContext.ShiftAssignmentEntries.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAssignmentEntriesAsync_WhenLocationStatusAndRangeProvided_ReturnsMatchingEntries()
    {
        await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(startAtUtc: new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero)),
            TestContext.Current.CancellationToken
        );
        await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(startAtUtc: new DateTimeOffset(2026, 6, 3, 16, 0, 0, TimeSpan.Zero)),
            TestContext.Current.CancellationToken
        );

        var result = await _assignmentService.GetAssignmentEntriesAsync(
            new AssignmentEntryQueryParams
            {
                LocationId = 5,
                StatusTypeCode = CalendarEventStatusTypeCodes.Active,
                StartAtUtc = new DateTimeOffset(2026, 6, 1, 15, 0, 0, TimeSpan.Zero),
                EndAtUtc = new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero),
            },
            TestContext.Current.CancellationToken
        );

        var entry = Assert.Single(result);
        Assert.Equal(new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero), entry.StartAtUtc);
        Assert.Equal(5, entry.LocationId);
    }

    [Fact]
    public async Task GetAssignmentSeriesAsync_WhenRangeProvided_ReturnsSeriesWithActiveOverlappingEntries()
    {
        var matching = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=2"),
            TestContext.Current.CancellationToken
        );
        await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(
                recurrenceRule: "FREQ=DAILY;COUNT=1",
                startAtUtc: new DateTimeOffset(2026, 6, 5, 16, 0, 0, TimeSpan.Zero)
            ),
            TestContext.Current.CancellationToken
        );

        var result = await _assignmentService.GetAssignmentSeriesAsync(
            new AssignmentSeriesQueryParams
            {
                LocationId = 5,
                StatusTypeCode = CalendarEventStatusTypeCodes.Active,
                StartAtUtc = new DateTimeOffset(2026, 6, 2, 15, 0, 0, TimeSpan.Zero),
                EndAtUtc = new DateTimeOffset(2026, 6, 2, 23, 0, 0, TimeSpan.Zero),
            },
            TestContext.Current.CancellationToken
        );

        var series = Assert.Single(result);
        Assert.Equal(matching.Id, series.Id);
        Assert.NotEmpty(series.Entries);
        Assert.Contains(
            series.Entries,
            entry => entry.StartAtUtc == new DateTimeOffset(2026, 6, 2, 16, 0, 0, TimeSpan.Zero)
        );
    }

    [Fact]
    public async Task UpdateAssignmentSeriesAsync_WhenChildFieldIsOverridden_PreservesOverride()
    {
        var created = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(),
            TestContext.Current.CancellationToken
        );
        var overriddenEntry = await _dbContext
            .AssignmentEntries.OrderBy(x => x.Id)
            .FirstAsync(x => x.AssignmentSeriesId == created.Id, TestContext.Current.CancellationToken);
        overriddenEntry.Capacity = 5;
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _assignmentService.UpdateAssignmentSeriesAsync(
            created.Id,
            CreateAssignmentSeriesRequest(capacity: 3),
            TestContext.Current.CancellationToken
        );

        var capacities = await _dbContext
            .AssignmentEntries.Where(x => x.AssignmentSeriesId == created.Id)
            .OrderBy(x => x.Id)
            .Select(x => x.Capacity)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal([5, 3], capacities);
    }

    [Fact]
    public async Task ExpireAssignmentSeriesAsync_WhenFound_CancelsSeriesAndChildEntries()
    {
        var created = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(),
            TestContext.Current.CancellationToken
        );

        var result = await _assignmentService.ExpireAssignmentSeriesAsync(
            created.Id,
            new ExpireShiftRequest { CancellationReason = "done" },
            CancelledByUser,
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(result);
        var events = await _dbContext
            .Events.Where(x => x.EventSeriesId == created.EventSeriesId)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.All(events, x => Assert.Equal(CalendarEventStatusTypeCodes.Cancelled, x.StatusTypeCode));
    }

    [Fact]
    public async Task LinkShiftEntryAsync_WhenValid_CreatesLinkWithSelectedUsersWithoutCapacityEnforcement()
    {
        var shift = await AddShiftEntryAsync(userIds: [UserA, UserB]);
        var assignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(capacity: 1),
            TestContext.Current.CancellationToken
        );

        var result = await _shiftAssignmentService.LinkShiftEntryAsync(
            new ShiftAssignmentEntryRequest
            {
                ShiftEntryId = shift.Id,
                AssignmentEntryId = assignment.Id,
                UserIds = [UserA, UserB],
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(1, result.Capacity);
        Assert.Equal(2, result.AssignedUserCount);
        Assert.Equal([UserA, UserB], result.UserIds.Order().ToArray());
    }

    [Fact]
    public async Task LinkShiftEntryAsync_WhenDuplicate_ThrowsInvalidOperationException()
    {
        var shift = await AddShiftEntryAsync(userIds: [UserA]);
        var assignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(),
            TestContext.Current.CancellationToken
        );
        var request = new ShiftAssignmentEntryRequest
        {
            ShiftEntryId = shift.Id,
            AssignmentEntryId = assignment.Id,
            UserIds = [UserA],
        };

        await _shiftAssignmentService.LinkShiftEntryAsync(request, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _shiftAssignmentService.LinkShiftEntryAsync(request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task LinkShiftSeriesAsync_WhenRunTwice_IsIdempotentAndCreatesMissingLinks()
    {
        var shiftSeries = await AddShiftSeriesWithEntriesAsync();
        var assignmentSeries = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(),
            TestContext.Current.CancellationToken
        );

        var request = new ShiftAssignmentSeriesRequest
        {
            ShiftSeriesId = shiftSeries.Id,
            AssignmentSeriesId = assignmentSeries.Id,
            AssignedUserIds = [UserA],
        };

        var firstResult = await _shiftAssignmentService.LinkShiftSeriesAsync(
            request,
            TestContext.Current.CancellationToken
        );
        var secondResult = await _shiftAssignmentService.LinkShiftSeriesAsync(
            request,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(2, firstResult.EntryLinks.Count);
        Assert.Equal(2, secondResult.EntryLinks.Count);
        Assert.Equal(shiftSeries.Id, firstResult.ShiftSeriesId);
        Assert.Equal(assignmentSeries.Id, firstResult.AssignmentSeriesId);
        Assert.Equal([UserA], firstResult.AssignedUserIds);
        Assert.Equal(firstResult.EntryLinks.Select(link => link.Id), firstResult.ShiftAssignmentEntryIds);
        Assert.Equal(0, firstResult.ExceptionCount);
        Assert.Equal(2, await _dbContext.ShiftAssignmentEntries.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LinkShiftSeriesAsync_WhenSeriesLinked_CreatesParentSeriesLinkAndChildMetadata()
    {
        var shiftSeries = await AddShiftSeriesWithEntriesAsync();
        var assignmentSeries = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(),
            TestContext.Current.CancellationToken
        );

        var result = await LinkSeriesAsync(shiftSeries.Id, assignmentSeries.Id);

        var seriesLink = await _dbContext
            .ShiftAssignmentSeriesLinks.Include(link => link.Users)
            .Include(link => link.EntryLinks)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(shiftSeries.Id, seriesLink.ShiftSeriesId);
        Assert.Equal(assignmentSeries.Id, seriesLink.AssignmentSeriesId);
        Assert.Equal([UserA], seriesLink.Users.Select(user => user.UserId));
        Assert.Equal(2, seriesLink.EntryLinks.Count);
        Assert.All(result, link => Assert.Equal(seriesLink.Id, link.ShiftAssignmentSeriesLinkId));
        Assert.All(result, link => Assert.False(link.IsException));
    }

    [Fact]
    public async Task LinkShiftSeriesAsync_WhenParentUsersChange_UpdatesNonExceptionChildLinks()
    {
        var shiftSeries = await AddShiftSeriesWithEntriesAsync(userIds: [UserA, UserB]);
        var assignmentSeries = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(),
            TestContext.Current.CancellationToken
        );

        await LinkSeriesAsync(shiftSeries.Id, assignmentSeries.Id);
        await _shiftAssignmentService.LinkShiftSeriesAsync(
            new ShiftAssignmentSeriesRequest
            {
                ShiftSeriesId = shiftSeries.Id,
                AssignmentSeriesId = assignmentSeries.Id,
                AssignedUserIds = [UserA, UserB],
            },
            TestContext.Current.CancellationToken
        );

        var links = await _dbContext
            .ShiftAssignmentEntries.Include(link => link.Users)
            .OrderBy(link => link.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, links.Count);
        Assert.All(links, link => Assert.Equal([UserA, UserB], link.Users.Select(user => user.UserId).Order()));
        Assert.All(links, link => Assert.False(link.IsException));
    }

    [Fact]
    public async Task LinkShiftSeriesAsync_WhenChildIsException_PreservesChildUsersUntilTheyMatchParent()
    {
        var shiftSeries = await AddShiftSeriesWithEntriesAsync(userIds: [UserA, UserB]);
        var assignmentSeries = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(),
            TestContext.Current.CancellationToken
        );
        await LinkSeriesAsync(shiftSeries.Id, assignmentSeries.Id);
        var exceptionLink = await _dbContext
            .ShiftAssignmentEntries.OrderBy(link => link.Id)
            .FirstAsync(TestContext.Current.CancellationToken);

        await _shiftAssignmentService.UpsertShiftEntryLinkAsync(
            new ShiftAssignmentEntryRequest
            {
                ShiftEntryId = exceptionLink.ShiftEntryId,
                AssignmentEntryId = exceptionLink.AssignmentEntryId,
                UserIds = [UserB],
            },
            TestContext.Current.CancellationToken
        );

        await _shiftAssignmentService.LinkShiftSeriesAsync(
            new ShiftAssignmentSeriesRequest
            {
                ShiftSeriesId = shiftSeries.Id,
                AssignmentSeriesId = assignmentSeries.Id,
                AssignedUserIds = [UserA, UserB],
            },
            TestContext.Current.CancellationToken
        );

        var reloadedException = await _dbContext
            .ShiftAssignmentEntries.Include(link => link.Users)
            .SingleAsync(link => link.Id == exceptionLink.Id, TestContext.Current.CancellationToken);
        Assert.True(reloadedException.IsException);
        Assert.Equal([UserB], reloadedException.Users.Select(user => user.UserId));

        await _shiftAssignmentService.UpsertShiftEntryLinkAsync(
            new ShiftAssignmentEntryRequest
            {
                ShiftEntryId = reloadedException.ShiftEntryId,
                AssignmentEntryId = reloadedException.AssignmentEntryId,
                UserIds = [UserA, UserB],
            },
            TestContext.Current.CancellationToken
        );

        reloadedException = await _dbContext
            .ShiftAssignmentEntries.Include(link => link.Users)
            .SingleAsync(link => link.Id == exceptionLink.Id, TestContext.Current.CancellationToken);
        Assert.False(reloadedException.IsException);
        Assert.Equal([UserA, UserB], reloadedException.Users.Select(user => user.UserId).Order());
    }

    [Fact]
    public async Task LinkShiftSeriesAsync_WhenManualChildLinkAlreadyExists_ThrowsAndDoesNotOverwriteManualLink()
    {
        var shiftSeries = await AddShiftSeriesWithEntriesAsync(userIds: [UserA, UserB]);
        var assignmentSeries = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(),
            TestContext.Current.CancellationToken
        );
        var shiftEntry = await _dbContext
            .ShiftEntries.Where(entry => entry.ShiftSeriesId == shiftSeries.Id)
            .OrderBy(entry => entry.Id)
            .FirstAsync(TestContext.Current.CancellationToken);
        var assignmentEntry = assignmentSeries.Entries.OrderBy(entry => entry.Id).First();
        var manualLink = await AddShiftAssignmentEntryAsync(shiftEntry.Id, assignmentEntry.Id, [UserA]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _shiftAssignmentService.LinkShiftSeriesAsync(
                new ShiftAssignmentSeriesRequest
                {
                    ShiftSeriesId = shiftSeries.Id,
                    AssignmentSeriesId = assignmentSeries.Id,
                    AssignedUserIds = [UserB],
                },
                TestContext.Current.CancellationToken
            )
        );

        Assert.Contains("already manually linked", exception.Message);
        Assert.False(await _dbContext.ShiftAssignmentSeriesLinks.AnyAsync(TestContext.Current.CancellationToken));
        Assert.Single(await _dbContext.ShiftAssignmentEntries.ToListAsync(TestContext.Current.CancellationToken));
        var reloadedManualLink = await _dbContext
            .ShiftAssignmentEntries.Include(link => link.Users)
            .SingleAsync(link => link.Id == manualLink.Id, TestContext.Current.CancellationToken);
        Assert.Null(reloadedManualLink.ShiftAssignmentSeriesLinkId);
        Assert.False(reloadedManualLink.IsException);
        Assert.Equal([UserA], reloadedManualLink.Users.Select(user => user.UserId));
    }

    [Fact]
    public async Task LinkShiftSeriesAsync_WhenChildLinkBelongsToAnotherSeriesLink_Throws()
    {
        var shiftSeries = await AddShiftSeriesWithEntriesAsync();
        var assignmentSeries = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(),
            TestContext.Current.CancellationToken
        );
        var foreignShiftSeries = await AddShiftSeriesWithEntriesAsync();
        var foreignAssignmentSeries = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(title: "Foreign assignment series", assignmentDefinitionId: 2),
            TestContext.Current.CancellationToken
        );
        var foreignSeriesLink = new ShiftAssignmentSeriesLink
        {
            ShiftSeriesId = foreignShiftSeries.Id,
            AssignmentSeriesId = foreignAssignmentSeries.Id,
            Users = [new ShiftAssignmentSeriesLinkUser { UserId = UserA }],
        };
        _dbContext.ShiftAssignmentSeriesLinks.Add(foreignSeriesLink);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var shiftEntry = await _dbContext
            .ShiftEntries.Where(entry => entry.ShiftSeriesId == shiftSeries.Id)
            .OrderBy(entry => entry.Id)
            .FirstAsync(TestContext.Current.CancellationToken);
        var assignmentEntry = assignmentSeries.Entries.OrderBy(entry => entry.Id).First();
        var foreignChild = await AddShiftAssignmentEntryAsync(
            shiftEntry.Id,
            assignmentEntry.Id,
            [UserA],
            foreignSeriesLink
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _shiftAssignmentService.LinkShiftSeriesAsync(
                new ShiftAssignmentSeriesRequest
                {
                    ShiftSeriesId = shiftSeries.Id,
                    AssignmentSeriesId = assignmentSeries.Id,
                    AssignedUserIds = [UserA],
                },
                TestContext.Current.CancellationToken
            )
        );

        Assert.Contains("already linked by another series link", exception.Message);
        Assert.False(
            await _dbContext.ShiftAssignmentSeriesLinks.AnyAsync(
                link => link.ShiftSeriesId == shiftSeries.Id && link.AssignmentSeriesId == assignmentSeries.Id,
                TestContext.Current.CancellationToken
            )
        );
        var reloadedForeignChild = await _dbContext
            .ShiftAssignmentEntries.Include(link => link.Users)
            .SingleAsync(link => link.Id == foreignChild.Id, TestContext.Current.CancellationToken);
        Assert.Equal(foreignSeriesLink.Id, reloadedForeignChild.ShiftAssignmentSeriesLinkId);
        Assert.False(reloadedForeignChild.IsException);
        Assert.Equal([UserA], reloadedForeignChild.Users.Select(user => user.UserId));
    }

    [Fact]
    public async Task LinkShiftSeriesAsync_WhenGeneratedNonExceptionChildNoLongerOverlaps_RemovesChild()
    {
        var shiftSeries = await AddShiftSeriesWithEntriesAsync();
        var assignmentSeries = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(),
            TestContext.Current.CancellationToken
        );
        await LinkSeriesAsync(shiftSeries.Id, assignmentSeries.Id);
        var obsoleteLink = await _dbContext
            .ShiftAssignmentEntries.Include(link => link.ShiftEntry)
                .ThenInclude(entry => entry!.Event)
            .OrderBy(link => link.AssignmentEntryId)
            .LastAsync(TestContext.Current.CancellationToken);
        obsoleteLink.ShiftEntry!.Event!.StartAtUtc = obsoleteLink.ShiftEntry.Event.StartAtUtc.AddDays(10);
        obsoleteLink.ShiftEntry.Event.EndAtUtc = obsoleteLink.ShiftEntry.Event.EndAtUtc!.Value.AddDays(10);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _shiftAssignmentService.LinkShiftSeriesAsync(
            new ShiftAssignmentSeriesRequest
            {
                ShiftSeriesId = shiftSeries.Id,
                AssignmentSeriesId = assignmentSeries.Id,
                AssignedUserIds = [UserA],
            },
            TestContext.Current.CancellationToken
        );

        Assert.Single(result.EntryLinks);
        Assert.Single(await _dbContext.ShiftAssignmentEntries.ToListAsync(TestContext.Current.CancellationToken));
        var seriesLink = await _dbContext
            .ShiftAssignmentSeriesLinks.Include(link => link.Users)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal([UserA], seriesLink.Users.Select(user => user.UserId));
        Assert.DoesNotContain(
            await _dbContext.ShiftAssignmentEntries.ToListAsync(TestContext.Current.CancellationToken),
            link => link.Id == obsoleteLink.Id
        );
    }

    [Fact]
    public async Task LinkShiftSeriesAsync_WhenGeneratedExceptionChildNoLongerOverlaps_PreservesChild()
    {
        var shiftSeries = await AddShiftSeriesWithEntriesAsync(userIds: [UserA, UserB]);
        var assignmentSeries = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(),
            TestContext.Current.CancellationToken
        );
        await LinkSeriesAsync(shiftSeries.Id, assignmentSeries.Id);
        var obsoleteExceptionLink = await _dbContext
            .ShiftAssignmentEntries.Include(link => link.Users)
            .Include(link => link.ShiftEntry)
                .ThenInclude(entry => entry!.Event)
            .OrderBy(link => link.AssignmentEntryId)
            .LastAsync(TestContext.Current.CancellationToken);
        obsoleteExceptionLink.IsException = true;
        obsoleteExceptionLink.Users.Clear();
        obsoleteExceptionLink.Users.Add(
            new ShiftAssignmentEntryUser { ShiftAssignmentEntryId = obsoleteExceptionLink.Id, UserId = UserB }
        );
        obsoleteExceptionLink.ShiftEntry!.Event!.StartAtUtc = obsoleteExceptionLink.ShiftEntry.Event.StartAtUtc.AddDays(
            10
        );
        obsoleteExceptionLink.ShiftEntry.Event.EndAtUtc =
            obsoleteExceptionLink.ShiftEntry.Event.EndAtUtc!.Value.AddDays(10);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _shiftAssignmentService.LinkShiftSeriesAsync(
            new ShiftAssignmentSeriesRequest
            {
                ShiftSeriesId = shiftSeries.Id,
                AssignmentSeriesId = assignmentSeries.Id,
                AssignedUserIds = [UserA],
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(2, result.EntryLinks.Count);
        Assert.Equal(1, result.ExceptionCount);
        var reloadedExceptionLink = await _dbContext
            .ShiftAssignmentEntries.Include(link => link.Users)
            .SingleAsync(link => link.Id == obsoleteExceptionLink.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(reloadedExceptionLink.ShiftAssignmentSeriesLinkId);
        Assert.True(reloadedExceptionLink.IsException);
        Assert.Equal([UserB], reloadedExceptionLink.Users.Select(user => user.UserId));
    }

    [Fact]
    public async Task LinkShiftSeriesAsync_WhenManualNonOverlapLinkExists_DoesNotRemoveManualLink()
    {
        var shiftSeries = await AddShiftSeriesWithEntriesAsync();
        var assignmentSeries = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(),
            TestContext.Current.CancellationToken
        );
        var nonOverlappingShift = await AddShiftEntryAsync(
            shiftSeriesId: shiftSeries.Id,
            startAtUtc: new DateTimeOffset(2026, 6, 10, 16, 0, 0, TimeSpan.Zero)
        );
        var nonOverlappingAssignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(
                assignmentSeriesId: assignmentSeries.Id,
                startAtUtc: new DateTimeOffset(2026, 6, 11, 16, 0, 0, TimeSpan.Zero)
            ),
            TestContext.Current.CancellationToken
        );
        var manualLink = await AddShiftAssignmentEntryAsync(
            nonOverlappingShift.Id,
            nonOverlappingAssignment.Id,
            [UserA]
        );

        var result = await _shiftAssignmentService.LinkShiftSeriesAsync(
            new ShiftAssignmentSeriesRequest
            {
                ShiftSeriesId = shiftSeries.Id,
                AssignmentSeriesId = assignmentSeries.Id,
                AssignedUserIds = [UserA],
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(2, result.EntryLinks.Count);
        var reloadedManualLink = await _dbContext
            .ShiftAssignmentEntries.Include(link => link.Users)
            .SingleAsync(link => link.Id == manualLink.Id, TestContext.Current.CancellationToken);
        Assert.Null(reloadedManualLink.ShiftAssignmentSeriesLinkId);
        Assert.False(reloadedManualLink.IsException);
        Assert.Equal([UserA], reloadedManualLink.Users.Select(user => user.UserId));
    }

    [Fact]
    public void AssignmentEntryRequest_DoesNotExposeIsException()
    {
        Assert.DoesNotContain(
            typeof(AssignmentEntryRequest).GetProperties(),
            property => property.Name == "IsException"
        );
        Assert.DoesNotContain(typeof(ShiftEntryRequest).GetProperties(), property => property.Name == "IsException");
    }

    [Fact]
    public void ShiftAssignmentLinkRequests_UseAssignedUserIds()
    {
        Assert.DoesNotContain(
            typeof(ShiftAssignmentSeriesRequest).GetProperties(),
            property => property.Name == "UserIds"
        );
        Assert.Contains(
            typeof(ShiftAssignmentSeriesRequest).GetProperties(),
            property => property.Name == "AssignedUserIds"
        );
        Assert.DoesNotContain(
            typeof(AssignmentSeriesLinkRequest).GetProperties(),
            property => property.Name == "UserIds"
        );
        Assert.Contains(
            typeof(AssignmentSeriesLinkRequest).GetProperties(),
            property => property.Name == "AssignedUserIds"
        );
        Assert.DoesNotContain(typeof(ShiftSeriesLinkRequest).GetProperties(), property => property.Name == "UserIds");
        Assert.Contains(typeof(ShiftSeriesLinkRequest).GetProperties(), property => property.Name == "AssignedUserIds");
        Assert.DoesNotContain(
            typeof(AssignmentEntryLinkRequest).GetProperties(),
            property => property.Name == "UserIds"
        );
        Assert.Contains(
            typeof(AssignmentEntryLinkRequest).GetProperties(),
            property => property.Name == "AssignedUserIds"
        );
        Assert.DoesNotContain(typeof(ShiftEntryLinkRequest).GetProperties(), property => property.Name == "UserIds");
        Assert.Contains(typeof(ShiftEntryLinkRequest).GetProperties(), property => property.Name == "AssignedUserIds");
    }

    [Fact]
    public async Task CreateAssignmentSeriesAsync_WhenLinkingSameShiftSeries_PreservesOtherAssignmentSeriesLinks()
    {
        var shiftSeries = await AddShiftSeriesWithEntriesAsync();
        var firstAssignmentSeries = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(),
            TestContext.Current.CancellationToken
        );
        await LinkSeriesAsync(shiftSeries.Id, firstAssignmentSeries.Id);

        await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(
                title: "Second assignment series",
                assignmentDefinitionId: 2,
                shiftSeriesIds: [shiftSeries.Id],
                assignedUserIds: [UserA]
            ),
            TestContext.Current.CancellationToken
        );

        var seriesLinks = await _dbContext
            .ShiftAssignmentSeriesLinks.OrderBy(link => link.AssignmentSeriesId)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, seriesLinks.Count);
        Assert.Contains(seriesLinks, link => link.AssignmentSeriesId == firstAssignmentSeries.Id);
    }

    [Fact]
    public async Task UpdateAssignmentEntryAsync_WhenSeriesBackedShiftLinksCleared_SuppressesOccurrenceLink()
    {
        var shiftSeries = await AddShiftSeriesWithEntriesAsync();
        var assignmentSeries = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(),
            TestContext.Current.CancellationToken
        );
        await LinkSeriesAsync(shiftSeries.Id, assignmentSeries.Id);
        var entryToUpdate = assignmentSeries.Entries.OrderBy(entry => entry.StartAtUtc).First();
        var untouchedEntry = assignmentSeries.Entries.OrderBy(entry => entry.StartAtUtc).Last();

        var result = await _assignmentService.UpdateAssignmentEntryAsync(
            entryToUpdate.Id,
            CreateAssignmentEntryRequest(
                assignmentSeriesId: assignmentSeries.Id,
                startAtUtc: entryToUpdate.StartAtUtc,
                endAtUtc: entryToUpdate.EndAtUtc,
                shiftEntryLinks: []
            ),
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(result);
        Assert.Empty(result.LinkedShiftEntryIds);
        Assert.Empty(result.AssignedUserIds);
        Assert.True(await _dbContext.ShiftAssignmentSeriesLinks.AnyAsync(TestContext.Current.CancellationToken));

        var suppressedLink = await _dbContext
            .ShiftAssignmentEntries.Include(link => link.Users)
            .SingleAsync(link => link.AssignmentEntryId == entryToUpdate.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(suppressedLink.ShiftAssignmentSeriesLinkId);
        Assert.True(suppressedLink.IsException);
        Assert.Empty(suppressedLink.Users);

        var reloaded = await _assignmentService.GetAssignmentEntryByIdAsync(
            entryToUpdate.Id,
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(reloaded);
        Assert.Empty(reloaded.LinkedShiftEntryIds);
        Assert.Empty(reloaded.AssignedUserIds);

        var untouched = await _assignmentService.GetAssignmentEntryByIdAsync(
            untouchedEntry.Id,
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(untouched);
        Assert.NotEmpty(untouched.LinkedShiftEntryIds);
        Assert.Equal([UserA], untouched.AssignedUserIds);

        await LinkSeriesAsync(shiftSeries.Id, assignmentSeries.Id);

        reloaded = await _assignmentService.GetAssignmentEntryByIdAsync(
            entryToUpdate.Id,
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(reloaded);
        Assert.Empty(reloaded.LinkedShiftEntryIds);
        Assert.Empty(reloaded.AssignedUserIds);
    }

    [Fact]
    public async Task UpdateAssignmentEntryAsync_WhenOneOfMultipleSeriesBackedShiftLinksRemoved_KeepsRequestedLink()
    {
        var firstShiftSeries = await AddShiftSeriesWithEntriesAsync();
        var secondShiftSeries = await AddShiftSeriesWithEntriesAsync();
        var assignmentSeries = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(),
            TestContext.Current.CancellationToken
        );
        await LinkSeriesAsync(firstShiftSeries.Id, assignmentSeries.Id);
        await LinkSeriesAsync(secondShiftSeries.Id, assignmentSeries.Id);
        var entryToUpdate = assignmentSeries.Entries.OrderBy(entry => entry.StartAtUtc).First();
        var existingLinks = await _dbContext
            .ShiftAssignmentEntries.Include(link => link.Users)
            .Where(link => link.AssignmentEntryId == entryToUpdate.Id)
            .OrderBy(link => link.ShiftEntryId)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, existingLinks.Count);
        var keptLink = existingLinks[0];
        var removedLink = existingLinks[1];

        var result = await _assignmentService.UpdateAssignmentEntryAsync(
            entryToUpdate.Id,
            CreateAssignmentEntryRequest(
                assignmentSeriesId: assignmentSeries.Id,
                startAtUtc: entryToUpdate.StartAtUtc,
                endAtUtc: entryToUpdate.EndAtUtc,
                shiftEntryLinks:
                [
                    new ShiftEntryLinkRequest { ShiftEntryId = keptLink.ShiftEntryId, AssignedUserIds = [UserA] },
                ]
            ),
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(result);
        Assert.Equal([keptLink.ShiftEntryId], result.LinkedShiftEntryIds);
        Assert.Equal([UserA], result.AssignedUserIds);

        var reloadedLinks = await _dbContext
            .ShiftAssignmentEntries.Include(link => link.Users)
            .Where(link => link.AssignmentEntryId == entryToUpdate.Id)
            .OrderBy(link => link.ShiftEntryId)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, reloadedLinks.Count);
        var reloadedKeptLink = Assert.Single(reloadedLinks, link => link.ShiftEntryId == keptLink.ShiftEntryId);
        Assert.False(reloadedKeptLink.IsException);
        Assert.Equal([UserA], reloadedKeptLink.Users.Select(user => user.UserId));
        var reloadedRemovedLink = Assert.Single(reloadedLinks, link => link.ShiftEntryId == removedLink.ShiftEntryId);
        Assert.True(reloadedRemovedLink.IsException);
        Assert.Empty(reloadedRemovedLink.Users);

        Assert.Equal(2, await _dbContext.ShiftAssignmentSeriesLinks.CountAsync(TestContext.Current.CancellationToken));
        var otherEntryIds = assignmentSeries
            .Entries.Where(entry => entry.Id != entryToUpdate.Id)
            .Select(entry => entry.Id);
        var otherEntryLinks = await _dbContext
            .ShiftAssignmentEntries.Include(link => link.Users)
            .Where(link => otherEntryIds.Contains(link.AssignmentEntryId))
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, otherEntryLinks.Count);
        Assert.All(otherEntryLinks, link => Assert.Equal([UserA], link.Users.Select(user => user.UserId)));
    }

    [Fact]
    public async Task LinkShiftSeriesAsync_WhenFiveDailyEntriesOverlap_CreatesOneLinkPerAssignmentEntry()
    {
        var startTimes = Enumerable
            .Range(0, 5)
            .Select(day => new DateTimeOffset(2026, 7, 14, 16, 0, 0, TimeSpan.Zero).AddDays(day))
            .ToArray();
        var shiftSeries = await AddShiftSeriesWithEntriesAsync(startTimes, shiftDuration: TimeSpan.FromHours(8));
        var assignmentSeries = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(
                recurrenceRule: "FREQ=DAILY;COUNT=5",
                startAtUtc: startTimes[0],
                endAtUtc: startTimes[0].AddHours(8),
                assignmentDefinitionId: 2
            ),
            TestContext.Current.CancellationToken
        );

        var result = await _shiftAssignmentService.LinkShiftSeriesAsync(
            new ShiftAssignmentSeriesRequest
            {
                ShiftSeriesId = shiftSeries.Id,
                AssignmentSeriesId = assignmentSeries.Id,
                AssignedUserIds = [UserA],
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(5, result.EntryLinks.Count);
        Assert.Equal(5, await _dbContext.ShiftAssignmentEntries.CountAsync(TestContext.Current.CancellationToken));
        var links = await _dbContext
            .ShiftAssignmentEntries.Include(link => link.ShiftEntry)
                .ThenInclude(entry => entry!.Event)
            .Include(link => link.AssignmentEntry)
                .ThenInclude(entry => entry!.Event)
            .Where(link => link.AssignmentEntry!.AssignmentSeriesId == assignmentSeries.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.All(links.GroupBy(link => link.AssignmentEntryId), group => Assert.Single(group));
        Assert.All(
            links,
            link => Assert.Equal(link.AssignmentEntry!.Event!.StartAtUtc, link.ShiftEntry!.Event!.StartAtUtc)
        );
    }

    [Fact]
    public async Task LinkShiftSeriesAsync_WhenShiftOvernightTouchesAssignmentLocalDateButDoesNotOverlap_CreatesNoLinks()
    {
        var shiftSeries = await AddShiftSeriesWithEntriesAsync([
            new DateTimeOffset(2026, 6, 2, 6, 0, 0, TimeSpan.Zero),
        ]);
        var assignmentSeries = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(
                recurrenceRule: "FREQ=DAILY;COUNT=1",
                startAtUtc: new DateTimeOffset(2026, 6, 2, 16, 0, 0, TimeSpan.Zero)
            ),
            TestContext.Current.CancellationToken
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _shiftAssignmentService.LinkShiftSeriesAsync(
                new ShiftAssignmentSeriesRequest
                {
                    ShiftSeriesId = shiftSeries.Id,
                    AssignmentSeriesId = assignmentSeries.Id,
                    AssignedUserIds = [UserA],
                },
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task LinkShiftSeriesAsync_WhenAllDayAssignmentUsesExclusiveEnd_DoesNotIncludeNextDay()
    {
        var shiftSeries = await AddShiftSeriesWithEntriesAsync([
            new DateTimeOffset(2026, 6, 2, 16, 0, 0, TimeSpan.Zero),
        ]);
        var assignmentSeries = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(
                recurrenceRule: "FREQ=DAILY;COUNT=1",
                startAtUtc: new DateTimeOffset(2026, 6, 1, 7, 0, 0, TimeSpan.Zero),
                endAtUtc: new DateTimeOffset(2026, 6, 2, 7, 0, 0, TimeSpan.Zero),
                allDay: true
            ),
            TestContext.Current.CancellationToken
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _shiftAssignmentService.LinkShiftSeriesAsync(
                new ShiftAssignmentSeriesRequest
                {
                    ShiftSeriesId = shiftSeries.Id,
                    AssignmentSeriesId = assignmentSeries.Id,
                    AssignedUserIds = [UserA],
                },
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task LinkShiftSeriesAsync_WhenLocalDatesDiffer_CreatesNoLinks()
    {
        var shiftSeries = await AddShiftSeriesWithEntriesAsync([
            new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
        ]);
        var assignmentSeries = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(
                recurrenceRule: "FREQ=DAILY;COUNT=1",
                startAtUtc: new DateTimeOffset(2026, 6, 2, 16, 0, 0, TimeSpan.Zero)
            ),
            TestContext.Current.CancellationToken
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _shiftAssignmentService.LinkShiftSeriesAsync(
                new ShiftAssignmentSeriesRequest
                {
                    ShiftSeriesId = shiftSeries.Id,
                    AssignmentSeriesId = assignmentSeries.Id,
                    AssignedUserIds = [UserA],
                },
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task LinkShiftSeriesAsync_WhenAssignmentDateRangeTouchesShiftLocalDate_CreatesLink()
    {
        var shiftSeries = await AddShiftSeriesWithEntriesAsync([
            new DateTimeOffset(2026, 6, 2, 16, 0, 0, TimeSpan.Zero),
        ]);
        var assignmentSeries = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(
                recurrenceRule: "FREQ=DAILY;COUNT=1",
                startAtUtc: new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
                endAtUtc: new DateTimeOffset(2026, 6, 3, 0, 0, 0, TimeSpan.Zero)
            ),
            TestContext.Current.CancellationToken
        );

        var result = await LinkSeriesAsync(shiftSeries.Id, assignmentSeries.Id);

        Assert.Single(result);
    }

    [Fact]
    public async Task LinkShiftSeriesAsync_WhenMultiDayShiftOverlapsSingleDayAssignment_CreatesLink()
    {
        var shiftSeries = await AddShiftSeriesWithEntriesAsync(
            [new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero)],
            shiftDuration: TimeSpan.FromHours(48)
        );
        var assignmentSeries = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(
                recurrenceRule: "FREQ=DAILY;COUNT=1",
                startAtUtc: new DateTimeOffset(2026, 6, 2, 16, 0, 0, TimeSpan.Zero)
            ),
            TestContext.Current.CancellationToken
        );

        var result = await LinkSeriesAsync(shiftSeries.Id, assignmentSeries.Id);

        Assert.Single(result);
    }

    [Fact]
    public async Task LinkShiftSeriesAsync_WhenUtcDateDiffersFromLocalDateButTimesDoNotOverlap_ThrowsInvalidOperationException()
    {
        var shiftSeries = await AddShiftSeriesWithEntriesAsync(
            [new DateTimeOffset(2026, 6, 2, 6, 30, 0, TimeSpan.Zero)],
            shiftDuration: TimeSpan.FromMinutes(30)
        );
        var assignmentSeries = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(
                recurrenceRule: "FREQ=DAILY;COUNT=1",
                startAtUtc: new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero)
            ),
            TestContext.Current.CancellationToken
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() => LinkSeriesAsync(shiftSeries.Id, assignmentSeries.Id));
    }

    [Fact]
    public async Task LinkShiftSeriesAsync_WhenTimesDoNotOverlapButLocalDateDoes_ThrowsInvalidOperationException()
    {
        var shiftSeries = await AddShiftSeriesWithEntriesAsync(
            [new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero)],
            shiftDuration: TimeSpan.FromHours(1)
        );
        var assignmentSeries = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(
                recurrenceRule: "FREQ=DAILY;COUNT=1",
                startAtUtc: new DateTimeOffset(2026, 6, 2, 2, 0, 0, TimeSpan.Zero),
                endAtUtc: new DateTimeOffset(2026, 6, 2, 3, 0, 0, TimeSpan.Zero)
            ),
            TestContext.Current.CancellationToken
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() => LinkSeriesAsync(shiftSeries.Id, assignmentSeries.Id));
    }

    [Fact]
    public async Task LinkShiftSeriesAsync_WhenAllDayAssignmentSameLocalDate_CreatesLink()
    {
        var shiftSeries = await AddShiftSeriesWithEntriesAsync([
            new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
        ]);
        var assignmentSeries = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(
                recurrenceRule: "FREQ=DAILY;COUNT=1",
                startAtUtc: new DateTimeOffset(2026, 6, 1, 7, 0, 0, TimeSpan.Zero),
                endAtUtc: new DateTimeOffset(2026, 6, 2, 7, 0, 0, TimeSpan.Zero),
                allDay: true
            ),
            TestContext.Current.CancellationToken
        );

        var result = await LinkSeriesAsync(shiftSeries.Id, assignmentSeries.Id);

        Assert.Single(result);
    }

    [Fact]
    public async Task LinkShiftSeriesAsync_WhenSelectedUserMissingFromIntersectingShift_ThrowsInvalidOperationException()
    {
        var shiftSeries = await AddShiftSeriesWithEntriesAsync(userIds: [UserA]);
        var assignmentSeries = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(),
            TestContext.Current.CancellationToken
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _shiftAssignmentService.LinkShiftSeriesAsync(
                new ShiftAssignmentSeriesRequest
                {
                    ShiftSeriesId = shiftSeries.Id,
                    AssignmentSeriesId = assignmentSeries.Id,
                    AssignedUserIds = [UserB],
                },
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task UpdateAssignmentSeriesAsync_WhenRecurrenceChanges_RegeneratesEntriesAndResetsOverrides()
    {
        var created = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=2"),
            TestContext.Current.CancellationToken
        );
        var overriddenEntry = await _dbContext
            .AssignmentEntries.OrderBy(entry => entry.Event!.SeriesStartAtUtc)
            .FirstAsync(entry => entry.AssignmentSeriesId == created.Id, TestContext.Current.CancellationToken);
        overriddenEntry.Capacity = 5;
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var originalEntries = await _dbContext
            .AssignmentEntries.Include(entry => entry.Event)
            .Where(entry => entry.AssignmentSeriesId == created.Id)
            .OrderBy(entry => entry.Event!.SeriesStartAtUtc)
            .Select(entry => new { entry.Id, EventId = entry.EventId })
            .ToListAsync(TestContext.Current.CancellationToken);

        var result = await _assignmentService.UpdateAssignmentSeriesAsync(
            created.Id,
            CreateAssignmentSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=3", capacity: 3),
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(result);
        Assert.Equal(3, result.AssignmentEntryIds.Count);
        var capacities = await _dbContext
            .AssignmentEntries.Include(entry => entry.Event)
            .Where(entry => entry.AssignmentSeriesId == created.Id)
            .OrderBy(entry => entry.Event!.SeriesStartAtUtc)
            .Select(entry => new
            {
                entry.Id,
                entry.EventId,
                entry.Capacity,
                entry.Event!.Title,
            })
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal([3, 3, 3], capacities.Select(entry => entry.Capacity).ToArray());
        Assert.All(capacities, entry => Assert.Equal("Assignment series", entry.Title));
        Assert.Empty(capacities.Select(entry => entry.Id).Intersect(originalEntries.Select(entry => entry.Id)));
        Assert.Empty(
            capacities.Select(entry => entry.EventId).Intersect(originalEntries.Select(entry => entry.EventId))
        );
        Assert.Equal(
            0,
            await _dbContext.AssignmentEntries.CountAsync(
                entry => originalEntries.Select(original => original.Id).Contains(entry.Id),
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task UpdateAssignmentSeriesAsync_WhenTitleChanges_PreservesEventTitleOverridesAndChildIds()
    {
        var created = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=2"),
            TestContext.Current.CancellationToken
        );
        var entries = await _dbContext
            .AssignmentEntries.Include(entry => entry.Event)
            .Where(entry => entry.AssignmentSeriesId == created.Id)
            .OrderBy(entry => entry.Event!.SeriesStartAtUtc)
            .ToListAsync(TestContext.Current.CancellationToken);
        entries[1].Event!.Title = "Custom assignment";
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _assignmentService.UpdateAssignmentSeriesAsync(
            created.Id,
            CreateAssignmentSeriesRequest(
                title: "Updated assignment series",
                recurrenceRule: "FREQ=DAILY;COUNT=2",
                capacity: 3
            ),
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(result);
        Assert.Equal(created.AssignmentEntryIds.Order().ToArray(), result.AssignmentEntryIds.Order().ToArray());
        var titles = await _dbContext
            .AssignmentEntries.Include(entry => entry.Event)
            .Where(entry => entry.AssignmentSeriesId == created.Id)
            .OrderBy(entry => entry.Event!.SeriesStartAtUtc)
            .Select(entry => entry.Event!.Title)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["Updated assignment series", "Custom assignment"], titles);
    }

    [Fact]
    public async Task UpdateAssignmentSeriesAsync_WhenAllCopiedFieldsChange_PreservesOnlyOverriddenEntryFields()
    {
        var created = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(),
            TestContext.Current.CancellationToken
        );
        var overriddenEntry = await _dbContext
            .AssignmentEntries.OrderBy(entry => entry.Id)
            .FirstAsync(entry => entry.AssignmentSeriesId == created.Id, TestContext.Current.CancellationToken);
        overriddenEntry.AssignmentDefinitionId = 2;
        overriddenEntry.Capacity = 5;
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _assignmentService.UpdateAssignmentSeriesAsync(
            created.Id,
            CreateAssignmentSeriesRequest(capacity: 3, assignmentDefinitionId: 2),
            TestContext.Current.CancellationToken
        );

        var entries = await _dbContext
            .AssignmentEntries.Where(entry => entry.AssignmentSeriesId == created.Id)
            .OrderBy(entry => entry.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, entries[0].AssignmentDefinitionId);
        Assert.Equal(5, entries[0].Capacity);
        Assert.Equal(2, entries[1].AssignmentDefinitionId);
        Assert.Equal(3, entries[1].Capacity);
    }

    [Fact]
    public async Task UpdateAssignmentSeriesAsync_WhenMaterializationFieldsChange_UpdatesChildrenWithoutDuplicates()
    {
        var created = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=2"),
            TestContext.Current.CancellationToken
        );
        var originalEntryIds = created.AssignmentEntryIds.Order().ToArray();

        var result = await _assignmentService.UpdateAssignmentSeriesAsync(
            created.Id,
            CreateAssignmentSeriesRequest(
                recurrenceRule: "FREQ=DAILY;COUNT=2",
                startAtUtc: new DateTimeOffset(2026, 6, 2, 7, 0, 0, TimeSpan.Zero),
                endAtUtc: new DateTimeOffset(2026, 6, 3, 7, 0, 0, TimeSpan.Zero),
                timeZoneId: "UTC",
                allDay: true
            ),
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(result);
        Assert.DoesNotContain(originalEntryIds[0], result.AssignmentEntryIds);
        Assert.DoesNotContain(originalEntryIds[1], result.AssignmentEntryIds);
        Assert.Equal(2, result.AssignmentEntryIds.Count);
        Assert.Equal(2, result.AssignmentEntryIds.Distinct().Count());
        var events = await _dbContext
            .AssignmentEntries.Include(entry => entry.Event)
            .Where(entry => entry.AssignmentSeriesId == created.Id)
            .Select(entry => entry.Event!)
            .OrderBy(e => e.SeriesStartAtUtc)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, events.Count);
        Assert.All(
            events,
            e =>
            {
                Assert.True(e.AllDay);
                Assert.Equal("UTC", e.TimeZoneId);
            }
        );
        Assert.Equal(new DateTimeOffset(2026, 6, 2, 7, 0, 0, TimeSpan.Zero), events[0].SeriesStartAtUtc);
    }

    [Fact]
    public async Task UpdateAssignmentSeriesAsync_WhenRecurrenceChangesAndSeriesHasLinks_Throws()
    {
        var created = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=3"),
            TestContext.Current.CancellationToken
        );
        var entries = await _dbContext
            .AssignmentEntries.Include(entry => entry.Event)
            .Where(entry => entry.AssignmentSeriesId == created.Id)
            .OrderBy(entry => entry.Event!.SeriesStartAtUtc)
            .ToListAsync(TestContext.Current.CancellationToken);
        var unlinkedRemovedEntryId = entries[1].Id;
        var linkedRemovedEntryId = entries[2].Id;
        var shift = await AddShiftEntryAsync(
            startAtUtc: new DateTimeOffset(2026, 6, 3, 16, 0, 0, TimeSpan.Zero),
            userIds: [UserA]
        );
        await _shiftAssignmentService.LinkShiftEntryAsync(
            new ShiftAssignmentEntryRequest
            {
                ShiftEntryId = shift.Id,
                AssignmentEntryId = linkedRemovedEntryId,
                UserIds = [UserA],
            },
            TestContext.Current.CancellationToken
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assignmentService.UpdateAssignmentSeriesAsync(
                created.Id,
                CreateAssignmentSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=1"),
                TestContext.Current.CancellationToken
            )
        );

        Assert.Contains("recurrence cannot be changed", exception.Message);
        Assert.True(
            await _dbContext.AssignmentEntries.AnyAsync(
                entry => entry.Id == unlinkedRemovedEntryId,
                TestContext.Current.CancellationToken
            )
        );
        Assert.NotEqual(
            CalendarEventStatusTypeCodes.Cancelled,
            (
                await _dbContext
                    .AssignmentEntries.Include(entry => entry.Event)
                    .SingleAsync(entry => entry.Id == linkedRemovedEntryId, TestContext.Current.CancellationToken)
            )
                .Event!
                .StatusTypeCode
        );
        Assert.True(
            await _dbContext.ShiftAssignmentEntryUsers.AnyAsync(
                user => user.ShiftAssignmentEntry!.AssignmentEntryId == linkedRemovedEntryId,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task UpdateAssignmentSeriesAsync_WhenRemovedOccurrenceHasNoLinks_RemovesObsoleteEntry()
    {
        var created = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=3"),
            TestContext.Current.CancellationToken
        );
        var entries = await _dbContext
            .AssignmentEntries.Include(entry => entry.Event)
            .Where(entry => entry.AssignmentSeriesId == created.Id)
            .OrderBy(entry => entry.Event!.SeriesStartAtUtc)
            .ToListAsync(TestContext.Current.CancellationToken);
        var unlinkedRemovedEntryId = entries[1].Id;

        await _assignmentService.UpdateAssignmentSeriesAsync(
            created.Id,
            CreateAssignmentSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=1"),
            TestContext.Current.CancellationToken
        );

        Assert.False(
            await _dbContext.AssignmentEntries.AnyAsync(
                entry => entry.Id == unlinkedRemovedEntryId,
                TestContext.Current.CancellationToken
            )
        );
        Assert.Equal(1, await _dbContext.AssignmentEntries.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateAssignmentSeriesAsync_WhenCancelledHistoricalEntryExists_DoesNotMutateIt()
    {
        var created = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=3"),
            TestContext.Current.CancellationToken
        );
        var entries = await _dbContext
            .AssignmentEntries.Include(entry => entry.Event)
            .Where(entry => entry.AssignmentSeriesId == created.Id)
            .OrderBy(entry => entry.Event!.SeriesStartAtUtc)
            .ToListAsync(TestContext.Current.CancellationToken);
        var linkedHistoricalEntry = entries[2];
        linkedHistoricalEntry.Event!.Title = "Historical title";
        linkedHistoricalEntry.Event.Notes = "Historical notes";
        linkedHistoricalEntry.AssignmentDefinitionId = 2;
        linkedHistoricalEntry.Capacity = 9;
        var shift = await AddShiftEntryAsync(startAtUtc: linkedHistoricalEntry.Event.StartAtUtc, userIds: [UserA]);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        await _shiftAssignmentService.LinkShiftEntryAsync(
            new ShiftAssignmentEntryRequest
            {
                ShiftEntryId = shift.Id,
                AssignmentEntryId = linkedHistoricalEntry.Id,
                UserIds = [UserA],
            },
            TestContext.Current.CancellationToken
        );

        var recurrenceException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assignmentService.UpdateAssignmentSeriesAsync(
                created.Id,
                CreateAssignmentSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=1"),
                TestContext.Current.CancellationToken
            )
        );
        Assert.Contains("recurrence cannot be changed", recurrenceException.Message);

        await _assignmentService.UpdateAssignmentSeriesAsync(
            created.Id,
            CreateAssignmentSeriesRequest(
                title: "Updated current series",
                recurrenceRule: "FREQ=DAILY;COUNT=3",
                capacity: 4,
                assignmentDefinitionId: 1
            ),
            TestContext.Current.CancellationToken
        );

        var historicalEntry = await _dbContext
            .AssignmentEntries.Include(entry => entry.Event)
            .SingleAsync(entry => entry.Id == linkedHistoricalEntry.Id, TestContext.Current.CancellationToken);
        Assert.NotEqual(CalendarEventStatusTypeCodes.Cancelled, historicalEntry.Event!.StatusTypeCode);
        Assert.Equal("Historical title", historicalEntry.Event.Title);
        Assert.Equal("Historical notes", historicalEntry.Event.Notes);
        Assert.Equal(2, historicalEntry.AssignmentDefinitionId);
        Assert.Equal(9, historicalEntry.Capacity);

        var currentEntry = await _dbContext
            .AssignmentEntries.Include(entry => entry.Event)
            .FirstAsync(
                entry =>
                    entry.AssignmentSeriesId == created.Id
                    && entry.Id != linkedHistoricalEntry.Id
                    && entry.Event!.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled,
                TestContext.Current.CancellationToken
            );
        Assert.Equal("Updated current series", currentEntry.Event!.Title);
        Assert.Equal(4, currentEntry.Capacity);
    }

    [Fact]
    public async Task LinkShiftEntryAsync_WhenInvalidRequest_ThrowsInvalidOperationException()
    {
        var shift = await AddShiftEntryAsync(userIds: [UserA]);
        var assignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(),
            TestContext.Current.CancellationToken
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _shiftAssignmentService.LinkShiftEntryAsync(
                new ShiftAssignmentEntryRequest
                {
                    ShiftEntryId = shift.Id,
                    AssignmentEntryId = assignment.Id,
                    UserIds = [],
                },
                TestContext.Current.CancellationToken
            )
        );
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _shiftAssignmentService.LinkShiftEntryAsync(
                new ShiftAssignmentEntryRequest
                {
                    ShiftEntryId = shift.Id,
                    AssignmentEntryId = assignment.Id,
                    UserIds = [UserA, UserA],
                },
                TestContext.Current.CancellationToken
            )
        );
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _shiftAssignmentService.LinkShiftEntryAsync(
                new ShiftAssignmentEntryRequest
                {
                    ShiftEntryId = shift.Id,
                    AssignmentEntryId = assignment.Id,
                    UserIds = [UserB],
                },
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task LinkShiftEntryAsync_WhenShiftOrAssignmentCancelled_RejectsNewLinksButPreservesExistingLinks()
    {
        var shift = await AddShiftEntryAsync(userIds: [UserA]);
        var assignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(),
            TestContext.Current.CancellationToken
        );
        await _shiftAssignmentService.LinkShiftEntryAsync(
            new ShiftAssignmentEntryRequest
            {
                ShiftEntryId = shift.Id,
                AssignmentEntryId = assignment.Id,
                UserIds = [UserA],
            },
            TestContext.Current.CancellationToken
        );
        shift.Event!.StatusTypeCode = CalendarEventStatusTypeCodes.Cancelled;
        await _assignmentService.ExpireAssignmentEntryAsync(
            assignment.Id,
            new ExpireShiftRequest { CancellationReason = "cancelled" },
            CancelledByUser,
            TestContext.Current.CancellationToken
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var newShift = await AddShiftEntryAsync(userIds: [UserA]);
        var newAssignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(startAtUtc: new DateTimeOffset(2026, 6, 2, 16, 0, 0, TimeSpan.Zero)),
            TestContext.Current.CancellationToken
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _shiftAssignmentService.LinkShiftEntryAsync(
                new ShiftAssignmentEntryRequest
                {
                    ShiftEntryId = shift.Id,
                    AssignmentEntryId = newAssignment.Id,
                    UserIds = [UserA],
                },
                TestContext.Current.CancellationToken
            )
        );
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _shiftAssignmentService.LinkShiftEntryAsync(
                new ShiftAssignmentEntryRequest
                {
                    ShiftEntryId = newShift.Id,
                    AssignmentEntryId = assignment.Id,
                    UserIds = [UserA],
                },
                TestContext.Current.CancellationToken
            )
        );
        Assert.Equal(1, await _dbContext.ShiftAssignmentEntries.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetSchedulingCalendarDataAsync_WhenAssignmentsExist_ReturnsAssignmentMetadata()
    {
        var assignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(),
            TestContext.Current.CancellationToken
        );

        var result = await _shiftService.GetSchedulingCalendarDataAsync(
            new SchedulingCalendarRequest
            {
                StartDate = new DateOnly(2026, 6, 1),
                EndDate = new DateOnly(2026, 6, 2),
                LocationId = 5,
            },
            TestContext.Current.CancellationToken
        );

        var item = Assert.Single(result.Events);
        Assert.Equal(assignment.Id, item.AssignmentEntryId);
        Assert.Equal(SchedulingConstants.AssignmentEventTypeCode, item.EventTypeCode);
        Assert.Equal(10, item.AssignmentCategoryTypeId);
        Assert.Equal("CourtRoom", item.AssignmentCategoryTypeCode);
        Assert.Equal(2, item.Capacity);
    }

    private async Task SeedBaseDataAsync()
    {
        _dbContext.EventTypes.AddRange(
            CreateEventType(SchedulingConstants.ShiftEventTypeCode),
            CreateEventType(SchedulingConstants.AssignmentEventTypeCode)
        );
        _dbContext.EventStatusTypes.AddRange(
            CreateStatusType(CalendarEventStatusTypeCodes.Draft),
            CreateStatusType(CalendarEventStatusTypeCodes.Active),
            CreateStatusType(CalendarEventStatusTypeCodes.Cancelled)
        );
        _dbContext.AssignmentCategoryTypes.Add(
            new AssignmentCategoryType
            {
                Id = 10,
                Code = "CourtRoom",
                Description = "Court Room",
                EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            }
        );
        _dbContext.AssignmentCategoryTypes.Add(
            new AssignmentCategoryType
            {
                Id = 11,
                Code = "EscortRun",
                Description = "Transport Assignment",
                EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            }
        );
        _dbContext.AssignmentSubCategoryTypes.Add(
            new AssignmentSubCategoryType
            {
                Id = 20,
                Code = "PROVINCIAL",
                Description = "Provincial",
                EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
                ParentCodeType = _dbContext.AssignmentCategoryTypes.Local.Single(type => type.Id == 10),
                ParentCodeTypeId = 10,
            }
        );
        _dbContext.AssignmentSubCategoryTypes.Add(
            new AssignmentSubCategoryType
            {
                Id = 21,
                Code = "SUPREME",
                Description = "Supreme",
                EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
                ParentCodeType = _dbContext.AssignmentCategoryTypes.Local.Single(type => type.Id == 10),
                ParentCodeTypeId = 10,
            }
        );
        _dbContext.AssignmentSubCategoryTypes.Add(
            new AssignmentSubCategoryType
            {
                Id = 30,
                Code = "IN_CUSTODY",
                Description = "In custody",
                EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
                ParentCodeType = _dbContext.AssignmentCategoryTypes.Local.Single(type => type.Id == 11),
                ParentCodeTypeId = 11,
            }
        );
        _dbContext.Locations.Add(
            new Location
            {
                Id = 5,
                AgencyId = "A5",
                Name = "Location 5",
                Timezone = "America/Vancouver",
            }
        );
        _dbContext.Users.AddRange(
            CreateUser(UserA, "UserA"),
            CreateUser(UserB, "UserB"),
            CreateUser(CancelledByUser, "CancelUser")
        );
        _dbContext.AssignmentDefinitions.AddRange(
            new AssignmentDefinition
            {
                Id = 1,
                LocationId = 5,
                Name = "STANDARD",
                Description = "Standard assignment",
                AssignmentCategoryTypeId = 10,
                AssignmentSubCategoryTypeId = 20,
                Color = "blue",
                DefaultStartTime = new TimeOnly(8, 0),
                DefaultEndTime = new TimeOnly(15, 0),
                DefaultCapacity = 2,
                EffectiveDateUtc = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            },
            new AssignmentDefinition
            {
                Id = 2,
                LocationId = 5,
                Name = "OVERRIDE",
                Description = "Override assignment",
                AssignmentCategoryTypeId = 11,
                AssignmentSubCategoryTypeId = 30,
                DefaultCapacity = 3,
                EffectiveDateUtc = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            },
            new AssignmentDefinition
            {
                Id = 3,
                LocationId = 5,
                Name = "FUTURE",
                Description = "Future assignment",
                AssignmentCategoryTypeId = 10,
                AssignmentSubCategoryTypeId = 20,
                DefaultCapacity = 1,
                EffectiveDateUtc = new DateTimeOffset(2026, 7, 22, 7, 0, 0, TimeSpan.Zero),
            }
        );

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<ShiftSeries> AddShiftSeriesWithEntriesAsync(
        IReadOnlyCollection<DateTimeOffset>? startTimesUtc = null,
        TimeSpan? shiftDuration = null,
        IReadOnlyCollection<Guid>? userIds = null
    )
    {
        var eventSeries = new EventSeries
        {
            Title = "Shift series",
            StartAtUtc = new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
            EventTypeCode = SchedulingConstants.ShiftEventTypeCode,
            StatusTypeCode = CalendarEventStatusTypeCodes.Active,
        };
        var shiftSeries = new ShiftSeries { EventSeries = eventSeries };
        _dbContext.ShiftSeries.Add(shiftSeries);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        foreach (
            var startAtUtc in startTimesUtc
                ??
                [
                    new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 6, 2, 16, 0, 0, TimeSpan.Zero),
                ]
        )
        {
            await AddShiftEntryAsync(
                shiftSeriesId: shiftSeries.Id,
                eventSeriesId: eventSeries.Id,
                startAtUtc: startAtUtc,
                endAtUtc: startAtUtc.Add(shiftDuration ?? TimeSpan.FromHours(8)),
                userIds: userIds ?? [UserA]
            );
        }

        return shiftSeries;
    }

    private async Task<ShiftEntry> AddShiftEntryAsync(
        int? shiftSeriesId = null,
        int? eventSeriesId = null,
        DateTimeOffset? startAtUtc = null,
        DateTimeOffset? endAtUtc = null,
        IReadOnlyCollection<Guid>? userIds = null,
        string statusTypeCode = CalendarEventStatusTypeCodes.Active
    )
    {
        var start = startAtUtc ?? new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero);
        var shiftEntry = new ShiftEntry
        {
            ShiftSeriesId = shiftSeriesId,
            Event = new Event
            {
                EventSeriesId = eventSeriesId,
                Title = "Shift",
                StartAtUtc = start,
                EndAtUtc = endAtUtc ?? start.AddHours(8),
                SeriesStartAtUtc = eventSeriesId.HasValue ? start : null,
                SeriesEndAtUtc = eventSeriesId.HasValue ? endAtUtc ?? start.AddHours(8) : null,
                TimeZoneId = "America/Vancouver",
                EventTypeCode = SchedulingConstants.ShiftEventTypeCode,
                StatusTypeCode = statusTypeCode,
                SourceModule = SchedulingConstants.SourceModule,
                LocationId = 5,
            },
            Users = (userIds ?? [UserA]).Select(userId => new ShiftEntryUser { UserId = userId }).ToList(),
        };

        _dbContext.ShiftEntries.Add(shiftEntry);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return shiftEntry;
    }

    private async Task<IReadOnlyCollection<ShiftAssignmentEntryResponse>> LinkSeriesAsync(
        int shiftSeriesId,
        int assignmentSeriesId
    )
    {
        var result = await _shiftAssignmentService.LinkShiftSeriesAsync(
            new ShiftAssignmentSeriesRequest
            {
                ShiftSeriesId = shiftSeriesId,
                AssignmentSeriesId = assignmentSeriesId,
                AssignedUserIds = [UserA],
            },
            TestContext.Current.CancellationToken
        );

        return result.EntryLinks;
    }

    private async Task<ShiftAssignmentEntry> AddShiftAssignmentEntryAsync(
        int shiftEntryId,
        int assignmentEntryId,
        IReadOnlyCollection<Guid> userIds,
        ShiftAssignmentSeriesLink? seriesLink = null,
        bool isException = false
    )
    {
        var link = new ShiftAssignmentEntry
        {
            ShiftEntryId = shiftEntryId,
            AssignmentEntryId = assignmentEntryId,
            ShiftAssignmentSeriesLink = seriesLink,
            IsException = isException,
            Users = userIds.Select(userId => new ShiftAssignmentEntryUser { UserId = userId }).ToList(),
        };
        _dbContext.ShiftAssignmentEntries.Add(link);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return link;
    }

    private static AssignmentEntryRequest CreateAssignmentEntryRequest(
        string title = "Assignment",
        int? capacity = 2,
        int? assignmentSeriesId = null,
        DateTimeOffset? startAtUtc = null,
        DateTimeOffset? endAtUtc = null,
        IReadOnlyCollection<int>? shiftEntryIds = null,
        IReadOnlyCollection<Guid>? assignedUserIds = null,
        IReadOnlyCollection<ShiftEntryLinkRequest>? shiftEntryLinks = null,
        string? color = null,
        int assignmentDefinitionId = 1
    )
    {
        var start = startAtUtc ?? new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero);
        return new AssignmentEntryRequest
        {
            AssignmentSeriesId = assignmentSeriesId,
            Title = title,
            StartAtUtc = start,
            EndAtUtc = endAtUtc ?? start.AddHours(7),
            TimeZoneId = "America/Vancouver",
            LocationId = 5,
            AssignmentDefinitionId = assignmentDefinitionId,
            Capacity = capacity,
            Color = color,
            ShiftEntryIds = shiftEntryLinks is null ? shiftEntryIds ?? [] : null,
            AssignedUserIds = assignedUserIds,
            ShiftEntryLinks = shiftEntryLinks,
        };
    }

    private AssignmentService CreateConflictAwareAssignmentService() =>
        new(
            NullLogger<AssignmentService>.Instance,
            _dbContext,
            new EventSeriesMaterializationService(
                _dbContext,
                new IcalNetRecurrenceRuleValidator(
                    new IcalNetRecurrenceExpander(CreateCalendarDateTimeService()),
                    CreateCalendarDateTimeService()
                ),
                new IcalNetRecurrenceExpander(CreateCalendarDateTimeService())
            ),
            new AssignmentSeriesMaterializationHandler(_dbContext),
            _shiftAssignmentService,
            new CalendarLifecycleService(),
            new CalendarConflictService(
                new CalendarConflictDetector(),
                [new SchedulingConflictParticipantProvider(_dbContext)],
                _dbContext
            )
        );

    private sealed class AllowAllCalendarConflictService : ICalendarConflictService
    {
        public IReadOnlyCollection<CalendarConflict> DetectConflicts(
            IReadOnlyCollection<CalendarConflictParticipant> participants
        ) => [];

        public Task<IReadOnlyCollection<CalendarConflict>> GetConflictsAsync(
            CalendarConflictQuery query,
            CancellationToken cancellationToken = default
        ) => Task.FromResult<IReadOnlyCollection<CalendarConflict>>([]);

        public Task<IReadOnlyCollection<CalendarConflict>> CheckCandidatesAsync(
            IReadOnlyCollection<CalendarConflictParticipant> candidates,
            CalendarConflictQuery query,
            CancellationToken cancellationToken = default
        ) => Task.FromResult<IReadOnlyCollection<CalendarConflict>>([]);

        public Task<CalendarConflictOverrideResponse> CreateOverrideAsync(
            CalendarConflictOverrideRequest request,
            Guid? createdById,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task InvalidateResolvedOverridesAsync(
            IReadOnlyCollection<int> eventIds,
            Guid? updatedById = null,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;
    }

    private static AssignmentSeriesRequest CreateAssignmentSeriesRequest(
        string title = "Assignment series",
        int? capacity = 2,
        string recurrenceRule = "FREQ=DAILY;COUNT=2",
        DateTimeOffset? startAtUtc = null,
        DateTimeOffset? endAtUtc = null,
        bool allDay = false,
        string timeZoneId = "America/Vancouver",
        int assignmentDefinitionId = 1,
        IReadOnlyCollection<int>? shiftSeriesIds = null,
        IReadOnlyCollection<Guid>? assignedUserIds = null,
        IReadOnlyCollection<ShiftSeriesLinkRequest>? shiftSeriesLinks = null,
        string? color = null
    )
    {
        var start = startAtUtc ?? new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero);
        return new AssignmentSeriesRequest
        {
            AssignmentDefinitionId = assignmentDefinitionId,
            Title = title,
            RecurrenceRule = recurrenceRule,
            StartAtUtc = start,
            EndAtUtc = endAtUtc ?? start.AddHours(7),
            TimeZoneId = timeZoneId,
            LocationId = 5,
            AllDay = allDay,
            Capacity = capacity,
            Color = color,
            ShiftSeriesIds = shiftSeriesIds ?? [],
            AssignedUserIds = assignedUserIds,
            ShiftSeriesLinks = shiftSeriesLinks,
        };
    }

    private static EventType CreateEventType(string code) =>
        new()
        {
            Code = code,
            Description = code,
            EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };

    private static EventStatusType CreateStatusType(string code) =>
        new()
        {
            Code = code,
            Description = code,
            EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };

    private static User CreateUser(Guid id, string name) =>
        new()
        {
            Id = id,
            IdirName = name,
            IsEnabled = true,
            FirstName = name,
            LastName = "Test",
            Email = $"{name}@example.com",
            Gender = Gender.Other,
        };

    private static CalendarDateTimeService CreateCalendarDateTimeService() =>
        new(Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "America/Vancouver" }));
}
