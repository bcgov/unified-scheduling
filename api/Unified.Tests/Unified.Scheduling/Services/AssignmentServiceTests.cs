using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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

        _assignmentService = new AssignmentService(
            NullLogger<AssignmentService>.Instance,
            _dbContext,
            materializationService,
            new AssignmentSeriesMaterializationHandler(_dbContext),
            new CalendarLifecycleService()
        );
        _shiftAssignmentService = new ShiftAssignmentService(
            NullLogger<ShiftAssignmentService>.Instance,
            _dbContext,
            calendarDateTimeService
        );
        _shiftService = new ShiftService(
            NullLogger<ShiftService>.Instance,
            _dbContext,
            materializationService,
            new ShiftSeriesMaterializationHandler(_dbContext),
            _shiftAssignmentService,
            calendarDateTimeService,
            new CalendarLifecycleService()
        );
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
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
        Assert.Equal(entry.Event.StartAtUtc, result.StartAtUtc);
        Assert.Equal(entry.Event.EndAtUtc, result.EndAtUtc);
        Assert.Equal(entry.Event.LocationId, result.LocationId);
        Assert.Equal(CalendarEventStatusTypeCodes.Active, result.StatusTypeCode);
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
                Assert.Equal(10, entry.AssignmentCategoryTypeId);
                Assert.Equal(20, entry.AssignmentSubCategoryTypeId);
                Assert.Equal(30, entry.AssignmentTypeId);
                Assert.Equal(2, entry.Capacity);
                Assert.Equal(CalendarEventStatusTypeCodes.Active, entry.Event!.StatusTypeCode);
            }
        );
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
            UserIds = [UserA],
        };

        var firstResult = await _shiftAssignmentService.LinkShiftSeriesAsync(
            request,
            TestContext.Current.CancellationToken
        );
        var secondResult = await _shiftAssignmentService.LinkShiftSeriesAsync(
            request,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(2, firstResult.Count);
        Assert.Empty(secondResult);
        Assert.Equal(2, await _dbContext.ShiftAssignmentEntries.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LinkShiftSeriesAsync_WhenShiftOvernightTouchesAssignmentLocalDate_CreatesLink()
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

        var result = await _shiftAssignmentService.LinkShiftSeriesAsync(
            new ShiftAssignmentSeriesRequest
            {
                ShiftSeriesId = shiftSeries.Id,
                AssignmentSeriesId = assignmentSeries.Id,
                UserIds = [UserA],
            },
            TestContext.Current.CancellationToken
        );

        var link = Assert.Single(result);
        Assert.Equal(assignmentSeries.AssignmentEntryIds.Single(), link.AssignmentEntryId);
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

        var result = await _shiftAssignmentService.LinkShiftSeriesAsync(
            new ShiftAssignmentSeriesRequest
            {
                ShiftSeriesId = shiftSeries.Id,
                AssignmentSeriesId = assignmentSeries.Id,
                UserIds = [UserA],
            },
            TestContext.Current.CancellationToken
        );

        Assert.Empty(result);
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

        var result = await _shiftAssignmentService.LinkShiftSeriesAsync(
            new ShiftAssignmentSeriesRequest
            {
                ShiftSeriesId = shiftSeries.Id,
                AssignmentSeriesId = assignmentSeries.Id,
                UserIds = [UserA],
            },
            TestContext.Current.CancellationToken
        );

        Assert.Empty(result);
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
    public async Task LinkShiftSeriesAsync_WhenUtcDateDiffersFromLocalDate_UsesLocalDate()
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

        var result = await LinkSeriesAsync(shiftSeries.Id, assignmentSeries.Id);

        Assert.Single(result);
    }

    [Fact]
    public async Task LinkShiftSeriesAsync_WhenTimesDoNotOverlapButLocalDateDoes_CreatesLink()
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

        var result = await LinkSeriesAsync(shiftSeries.Id, assignmentSeries.Id);

        Assert.Single(result);
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
                    UserIds = [UserB],
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
        overriddenEntry.AssignmentCategoryTypeId = 11;
        overriddenEntry.AssignmentSubCategoryTypeId = 21;
        overriddenEntry.AssignmentTypeId = 31;
        overriddenEntry.Capacity = 5;
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _assignmentService.UpdateAssignmentSeriesAsync(
            created.Id,
            CreateAssignmentSeriesRequest(
                capacity: 3,
                assignmentCategoryTypeId: 11,
                assignmentSubCategoryTypeId: 21,
                assignmentTypeId: 31
            ),
            TestContext.Current.CancellationToken
        );

        var entries = await _dbContext
            .AssignmentEntries.Where(entry => entry.AssignmentSeriesId == created.Id)
            .OrderBy(entry => entry.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(11, entries[0].AssignmentCategoryTypeId);
        Assert.Equal(21, entries[0].AssignmentSubCategoryTypeId);
        Assert.Equal(31, entries[0].AssignmentTypeId);
        Assert.Equal(5, entries[0].Capacity);
        Assert.Equal(11, entries[1].AssignmentCategoryTypeId);
        Assert.Equal(21, entries[1].AssignmentSubCategoryTypeId);
        Assert.Equal(31, entries[1].AssignmentTypeId);
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
    public async Task UpdateAssignmentSeriesAsync_WhenRemovedOccurrenceHasLinks_CancelsLinkedEntryAndDeletesUnlinkedEntry()
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
        var linkedEntry = await _dbContext
            .AssignmentEntries.Include(entry => entry.Event)
            .Include(entry => entry.ShiftAssignmentEntries)
            .SingleAsync(entry => entry.Id == linkedRemovedEntryId, TestContext.Current.CancellationToken);
        Assert.Equal(CalendarEventStatusTypeCodes.Cancelled, linkedEntry.Event!.StatusTypeCode);
        Assert.NotEmpty(linkedEntry.ShiftAssignmentEntries);
        Assert.True(
            await _dbContext.ShiftAssignmentEntryUsers.AnyAsync(
                user => user.ShiftAssignmentEntry!.AssignmentEntryId == linkedRemovedEntryId,
                TestContext.Current.CancellationToken
            )
        );
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
        linkedHistoricalEntry.AssignmentCategoryTypeId = 11;
        linkedHistoricalEntry.AssignmentSubCategoryTypeId = 21;
        linkedHistoricalEntry.AssignmentTypeId = 31;
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

        await _assignmentService.UpdateAssignmentSeriesAsync(
            created.Id,
            CreateAssignmentSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=1"),
            TestContext.Current.CancellationToken
        );
        await _assignmentService.UpdateAssignmentSeriesAsync(
            created.Id,
            CreateAssignmentSeriesRequest(
                title: "Updated current series",
                recurrenceRule: "FREQ=DAILY;COUNT=1",
                capacity: 4,
                assignmentCategoryTypeId: 10,
                assignmentSubCategoryTypeId: 20,
                assignmentTypeId: 30
            ),
            TestContext.Current.CancellationToken
        );

        var historicalEntry = await _dbContext
            .AssignmentEntries.Include(entry => entry.Event)
            .SingleAsync(entry => entry.Id == linkedHistoricalEntry.Id, TestContext.Current.CancellationToken);
        Assert.Equal(CalendarEventStatusTypeCodes.Cancelled, historicalEntry.Event!.StatusTypeCode);
        Assert.Equal("Historical title", historicalEntry.Event.Title);
        Assert.Equal("Historical notes", historicalEntry.Event.Notes);
        Assert.Equal(11, historicalEntry.AssignmentCategoryTypeId);
        Assert.Equal(21, historicalEntry.AssignmentSubCategoryTypeId);
        Assert.Equal(31, historicalEntry.AssignmentTypeId);
        Assert.Equal(9, historicalEntry.Capacity);

        var currentEntry = await _dbContext
            .AssignmentEntries.Include(entry => entry.Event)
            .SingleAsync(
                entry =>
                    entry.AssignmentSeriesId == created.Id
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
            }
        );
        _dbContext.AssignmentSubCategoryTypes.Add(
            new AssignmentSubCategoryType
            {
                Id = 21,
                Code = "SUPREME",
                Description = "Supreme",
                EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            }
        );
        _dbContext.AssignmentTypes.Add(
            new AssignmentType
            {
                Id = 30,
                LocationId = 5,
                Code = "CONTROL",
                Description = "Control",
                EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            }
        );
        _dbContext.AssignmentTypes.Add(
            new AssignmentType
            {
                Id = 31,
                LocationId = 5,
                Code = "SECURITY",
                Description = "Security",
                EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
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
    ) =>
        await _shiftAssignmentService.LinkShiftSeriesAsync(
            new ShiftAssignmentSeriesRequest
            {
                ShiftSeriesId = shiftSeriesId,
                AssignmentSeriesId = assignmentSeriesId,
                UserIds = [UserA],
            },
            TestContext.Current.CancellationToken
        );

    private static AssignmentEntryRequest CreateAssignmentEntryRequest(
        int capacity = 2,
        DateTimeOffset? startAtUtc = null,
        DateTimeOffset? endAtUtc = null
    )
    {
        var start = startAtUtc ?? new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero);
        return new AssignmentEntryRequest
        {
            Title = "Assignment",
            StartAtUtc = start,
            EndAtUtc = endAtUtc ?? start.AddHours(7),
            TimeZoneId = "America/Vancouver",
            LocationId = 5,
            AssignmentCategoryTypeId = 10,
            AssignmentSubCategoryTypeId = 20,
            AssignmentTypeId = 30,
            Capacity = capacity,
        };
    }

    private static AssignmentSeriesRequest CreateAssignmentSeriesRequest(
        string title = "Assignment series",
        int capacity = 2,
        string recurrenceRule = "FREQ=DAILY;COUNT=2",
        DateTimeOffset? startAtUtc = null,
        DateTimeOffset? endAtUtc = null,
        bool allDay = false,
        string timeZoneId = "America/Vancouver",
        int assignmentCategoryTypeId = 10,
        int assignmentSubCategoryTypeId = 20,
        int assignmentTypeId = 30
    )
    {
        var start = startAtUtc ?? new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero);
        return new AssignmentSeriesRequest
        {
            Title = title,
            RecurrenceRule = recurrenceRule,
            StartAtUtc = start,
            EndAtUtc = endAtUtc ?? start.AddHours(7),
            TimeZoneId = timeZoneId,
            LocationId = 5,
            AllDay = allDay,
            AssignmentCategoryTypeId = assignmentCategoryTypeId,
            AssignmentSubCategoryTypeId = assignmentSubCategoryTypeId,
            AssignmentTypeId = assignmentTypeId,
            Capacity = capacity,
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
