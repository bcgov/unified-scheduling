using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Unified.Calendar.Options;
using Unified.Calendar.Services;
using Unified.Common.Time;
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

public class ShiftServiceTests : IAsyncLifetime
{
    private static readonly Guid UserA = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserB = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid UserC = new("33333333-3333-3333-3333-333333333333");
    private static readonly Guid CancelledByUser = new("44444444-4444-4444-4444-444444444444");

    private SqliteConnection _connection = null!;
    private UnifiedDbContext _dbContext = null!;
    private ShiftService _service = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.CreateFunction("now", () => DateTimeOffset.UtcNow.ToString("O"));
        await _connection.OpenAsync(TestContext.Current.CancellationToken);

        var options = new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite(_connection).Options;
        _dbContext = new SqliteTestUnifiedDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await SeedBaseDataAsync();

        var timeZoneService = new TimeZoneService();
        var timeZoneResolver = CreateCalendarTimeZoneResolver();
        var recurrenceExpander = new IcalNetRecurrenceExpander(timeZoneService, timeZoneResolver);
        var recurrenceRuleValidator = new IcalNetRecurrenceRuleValidator(
            recurrenceExpander,
            timeZoneService,
            timeZoneResolver
        );
        var materializationService = new EventSeriesMaterializationService(
            _dbContext,
            recurrenceRuleValidator,
            recurrenceExpander
        );
        var materializationHandler = new ShiftSeriesMaterializationHandler(_dbContext);

        _service = new ShiftService(
            NullLogger<ShiftService>.Instance,
            _dbContext,
            materializationService,
            recurrenceExpander,
            materializationHandler,
            new ShiftAssignmentService(NullLogger<ShiftAssignmentService>.Instance, _dbContext),
            new CalendarLifecycleService(),
            TimeProvider.System
        );
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task CreateShiftSeriesAsync_WhenRequestHasMultipleUsers_CreatesEventSeriesShiftSeriesAndUsers()
    {
        // Arrange
        var request = CreateShiftSeriesRequest(userIds: [UserA, UserB, UserA]);

        // Act
        var result = await _service.CreateShiftSeriesAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([UserA, UserB], result.UserIds);

        var entity = await _dbContext
            .ShiftSeries.Include(x => x.EventSeries)
            .Include(x => x.Users)
            .SingleAsync(x => x.Id == result.Id, TestContext.Current.CancellationToken);
        Assert.Equal(result.EventSeriesId, entity.EventSeriesId);
        Assert.Equal("Series", entity.EventSeries!.Title);
        Assert.Equal(CalendarEventStatusTypeCodes.Draft, entity.EventSeries.StatusTypeCode);
        Assert.Equal(SchedulingConstants.ShiftEventTypeCode, entity.EventSeries.EventTypeCode);
        Assert.Equal([UserA, UserB], entity.Users.Select(x => x.UserId).Order().ToArray());

        var eventEntity = await _dbContext.Events.SingleAsync(
            x => x.EventSeriesId == result.EventSeriesId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(request.StartAtUtc, eventEntity.SeriesStartAtUtc);
        Assert.Equal(request.EndAtUtc, eventEntity.SeriesEndAtUtc);
        Assert.False(eventEntity.IsException);

        var shiftEntry = await _dbContext
            .ShiftEntries.Include(x => x.Users)
            .SingleAsync(x => x.ShiftSeriesId == result.Id, TestContext.Current.CancellationToken);
        Assert.Equal(eventEntity.Id, shiftEntry.EventId);
        Assert.Equal([UserA, UserB], shiftEntry.Users.Select(x => x.UserId).Order().ToArray());
        Assert.Equal([eventEntity.Id], result.EventIds);
        Assert.Equal([shiftEntry.Id], result.ShiftEntryIds);
    }

    [Fact]
    public async Task CreateShiftSeriesAsync_WhenMaterializedShiftAlreadyExists_AllowsDuplicate()
    {
        // Arrange
        var request = CreateShiftSeriesRequest();
        await _service.CreateShiftSeriesAsync(request, TestContext.Current.CancellationToken);

        // Act
        await _service.CreateShiftSeriesAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, await _dbContext.ShiftSeries.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, await _dbContext.ShiftEntries.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateShiftSeriesAsync_WhenRRuleIsUnbounded_ThrowsInvalidOperationExceptionAndRollsBack()
    {
        // Arrange
        var request = CreateShiftSeriesRequest(recurrenceRule: "FREQ=DAILY");

        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateShiftSeriesAsync(request, TestContext.Current.CancellationToken)
        );
        Assert.Empty(_dbContext.EventSeries);
        Assert.Empty(_dbContext.ShiftSeries);
        Assert.Empty(_dbContext.Events);
        Assert.Empty(_dbContext.ShiftEntries);
    }

    [Fact]
    public async Task UpdateShiftSeriesAsync_WhenRecurrenceChanges_DeletesExistingEntriesAndRecreatesEntries()
    {
        // Arrange
        var created = await _service.CreateShiftSeriesAsync(
            CreateShiftSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=3", userIds: [UserA, UserB]),
            TestContext.Current.CancellationToken
        );
        var firstEvent = await _dbContext
            .Events.OrderBy(x => x.SeriesStartAtUtc)
            .FirstAsync(x => x.EventSeriesId == created.EventSeriesId, TestContext.Current.CancellationToken);
        firstEvent.StartAtUtc = firstEvent.StartAtUtc.AddHours(1);
        CalendarEventExceptionHelper.UpdateExceptionFlag(firstEvent);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var originalEventIds = await _dbContext
            .Events.Where(x => x.EventSeriesId == created.EventSeriesId)
            .Select(x => x.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _service.UpdateShiftSeriesAsync(
            created.Id,
            CreateShiftSeriesRequest(title: "Updated", recurrenceRule: "FREQ=DAILY;COUNT=2", userIds: [UserB, UserC]),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result);

        var events = await _dbContext
            .Events.Where(x => x.EventSeriesId == created.EventSeriesId)
            .OrderBy(x => x.SeriesStartAtUtc)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, events.Count);
        Assert.All(
            events,
            x =>
            {
                Assert.False(x.IsException);
                Assert.Equal("Updated", x.Title);
                Assert.Equal(CalendarEventStatusTypeCodes.Draft, x.StatusTypeCode);
            }
        );

        var generatedEvent = events.First();
        var shiftEntry = await _dbContext
            .ShiftEntries.Include(x => x.Users)
            .SingleAsync(x => x.EventId == generatedEvent.Id, TestContext.Current.CancellationToken);
        Assert.Equal([UserB, UserC], shiftEntry.Users.Select(x => x.UserId).Order().ToArray());
        Assert.False(
            await _dbContext.Events.AnyAsync(
                x => originalEventIds.Contains(x.Id),
                TestContext.Current.CancellationToken
            )
        );
        Assert.False(
            await _dbContext.ShiftEntries.AnyAsync(
                x => originalEventIds.Contains(x.EventId),
                TestContext.Current.CancellationToken
            )
        );
        Assert.Equal(2, await _dbContext.ShiftEntries.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(events.Select(x => x.Id).Order().ToArray(), result.EventIds.Order().ToArray());
        var currentShiftEntryIds = await _dbContext
            .ShiftEntries.Where(x => events.Select(e => e.Id).Contains(x.EventId))
            .Select(x => x.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(currentShiftEntryIds.Order().ToArray(), result.ShiftEntryIds.Order().ToArray());
    }

    [Fact]
    public async Task UpdateShiftSeriesAsync_WhenMaterializedShiftCollides_AllowsDuplicate()
    {
        // Arrange
        var request = CreateShiftSeriesRequest();
        await _service.CreateShiftSeriesAsync(request, TestContext.Current.CancellationToken);
        var other = await _service.CreateShiftSeriesAsync(
            request with
            {
                Title = "Other",
                LocationId = 9,
            },
            TestContext.Current.CancellationToken
        );

        // Act
        var result = await _service.UpdateShiftSeriesAsync(other.Id, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(request.LocationId, result.LocationId);
    }

    [Fact]
    public async Task UpdateShiftSeriesAsync_WhenRecurrenceChangesAndSeriesIsNotDraft_ThrowsInvalidOperationException()
    {
        // Arrange
        var created = await _service.CreateShiftSeriesAsync(
            CreateShiftSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=2"),
            TestContext.Current.CancellationToken
        );
        await _service.PublishShiftSeriesAsync(created.Id, TestContext.Current.CancellationToken);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateShiftSeriesAsync(
                created.Id,
                CreateShiftSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=3"),
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Contains("must be in draft status to allow edits", exception.Message);
        var eventSeries = await _dbContext.EventSeries.SingleAsync(
            x => x.Id == created.EventSeriesId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal("FREQ=DAILY;COUNT=2", eventSeries.RecurrenceRule);
        Assert.Equal(
            2,
            await _dbContext.Events.CountAsync(
                x => x.EventSeriesId == created.EventSeriesId,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task UpdateShiftSeriesAsync_WhenSeriesIsDraftAndChildEventIsActive_ThrowsInvalidOperationException()
    {
        // Arrange
        var created = await _service.CreateShiftSeriesAsync(
            CreateShiftSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=2"),
            TestContext.Current.CancellationToken
        );
        var childEvent = await _dbContext.Events.FirstAsync(
            x => x.EventSeriesId == created.EventSeriesId,
            TestContext.Current.CancellationToken
        );
        childEvent.StatusTypeCode = CalendarEventStatusTypeCodes.Active;
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateShiftSeriesAsync(
                created.Id,
                CreateShiftSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=3"),
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Contains("cannot be recreated", exception.Message);
        Assert.Equal(
            2,
            await _dbContext.Events.CountAsync(
                x => x.EventSeriesId == created.EventSeriesId,
                TestContext.Current.CancellationToken
            )
        );
        Assert.Equal(CalendarEventStatusTypeCodes.Active, childEvent.StatusTypeCode);
    }

    [Fact]
    public async Task UpdateShiftSeriesAsync_WhenSeriesIsNotDraftAndRecurrenceIsUnchanged_ThrowsInvalidOperationException()
    {
        // Arrange
        var created = await _service.CreateShiftSeriesAsync(
            CreateShiftSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=2"),
            TestContext.Current.CancellationToken
        );
        await _service.PublishShiftSeriesAsync(created.Id, TestContext.Current.CancellationToken);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateShiftSeriesAsync(
                created.Id,
                CreateShiftSeriesRequest(title: "Updated", recurrenceRule: "FREQ=DAILY;COUNT=2", userIds: [UserB]),
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Contains("must be in draft status to allow edits", exception.Message);
        var eventSeries = await _dbContext.EventSeries.SingleAsync(
            x => x.Id == created.EventSeriesId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal("Series", eventSeries.Title);
    }

    [Fact]
    public async Task GetShiftSeriesAsync_WhenFiltersProvided_MatchesChildUsersAndEventSeries()
    {
        // Arrange
        var first = await _service.CreateShiftSeriesAsync(
            CreateShiftSeriesRequest(userIds: [UserA, UserB]),
            TestContext.Current.CancellationToken
        );
        await _service.CreateShiftSeriesAsync(
            CreateShiftSeriesRequest(title: "Other", userIds: [UserC]) with
            {
                LocationId = 9,
            },
            TestContext.Current.CancellationToken
        );

        // Act
        var result = await _service.GetShiftSeriesAsync(
            new ShiftSeriesQueryParams { EventSeriesId = first.EventSeriesId, UserId = UserB },
            TestContext.Current.CancellationToken
        );

        // Assert
        var item = Assert.Single(result);
        Assert.Equal(first.Id, item.Id);
        Assert.Equal([UserA, UserB], item.UserIds);
        Assert.Equal(first.EventIds, item.EventIds);
        Assert.Equal(first.ShiftEntryIds, item.ShiftEntryIds);
    }

    [Fact]
    public async Task GetShiftSeriesAsync_WhenCandidateRangeProvided_UsesRecurrenceAndExcludesInvalidCandidates()
    {
        // Arrange
        var matching = await _service.CreateShiftSeriesAsync(
            CreateShiftSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=3"),
            TestContext.Current.CancellationToken
        );
        var nonRecurring = await _service.CreateShiftSeriesAsync(
            CreateShiftSeriesRequest(title: "Non-recurring match") with
            {
                StartAtUtc = new DateTimeOffset(2026, 6, 2, 8, 0, 0, TimeSpan.Zero),
                EndAtUtc = new DateTimeOffset(2026, 6, 2, 10, 0, 0, TimeSpan.Zero),
            },
            TestContext.Current.CancellationToken
        );
        await _service.CreateShiftSeriesAsync(
            CreateShiftSeriesRequest(title: "Wrong location", recurrenceRule: "FREQ=DAILY;COUNT=3") with
            {
                LocationId = 9,
            },
            TestContext.Current.CancellationToken
        );
        await _service.CreateShiftSeriesAsync(
            CreateShiftSeriesRequest(title: "No occurrence on queried day", recurrenceRule: "FREQ=WEEKLY;COUNT=2") with
            {
                StartAtUtc = new DateTimeOffset(2026, 6, 1, 18, 0, 0, TimeSpan.Zero),
                EndAtUtc = new DateTimeOffset(2026, 6, 1, 22, 0, 0, TimeSpan.Zero),
            },
            TestContext.Current.CancellationToken
        );
        var cancelled = await _service.CreateShiftSeriesAsync(
            CreateShiftSeriesRequest(title: "Cancelled") with
            {
                StartAtUtc = new DateTimeOffset(2026, 6, 2, 18, 0, 0, TimeSpan.Zero),
                EndAtUtc = new DateTimeOffset(2026, 6, 2, 22, 0, 0, TimeSpan.Zero),
            },
            TestContext.Current.CancellationToken
        );
        var cancelledEventSeries = await _dbContext.EventSeries.SingleAsync(
            series => series.Id == cancelled.EventSeriesId,
            TestContext.Current.CancellationToken
        );
        cancelledEventSeries.StatusTypeCode = CalendarEventStatusTypeCodes.Cancelled;

        var unmaterializedEntry = await _dbContext
            .ShiftEntries.Include(shiftEntry => shiftEntry.Event)
            .SingleAsync(
                shiftEntry =>
                    shiftEntry.Event != null
                    && shiftEntry.Event.EventSeriesId == matching.EventSeriesId
                    && shiftEntry.Event.SeriesStartAtUtc == new DateTimeOffset(2026, 6, 2, 16, 0, 0, TimeSpan.Zero),
                TestContext.Current.CancellationToken
            );
        _dbContext.ShiftEntries.Remove(unmaterializedEntry);
        _dbContext.Events.Remove(unmaterializedEntry.Event);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _service.GetShiftSeriesAsync(
            new ShiftSeriesQueryParams
            {
                LocationId = 5,
                StartAtUtc = new DateTimeOffset(2026, 6, 2, 7, 0, 0, TimeSpan.Zero),
                EndAtUtc = new DateTimeOffset(2026, 6, 3, 7, 0, 0, TimeSpan.Zero),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([matching.Id, nonRecurring.Id], result.Select(series => series.Id).Order().ToArray());
    }

    [Theory]
    [InlineData(2, 10, 2, 12, 1, true)]
    [InlineData(1, 23, 2, 2, 1, true)]
    [InlineData(2, 23, 3, 2, 1, true)]
    [InlineData(1, 22, 2, 0, 1, false)]
    [InlineData(3, 0, 3, 2, 1, false)]
    [InlineData(1, 10, 1, 12, 1, false)]
    public async Task GetShiftSeriesAsync_WhenOccurrenceIsNotMaterialized_UsesHalfOpenIntervalOverlap(
        int startDay,
        int startHour,
        int endDay,
        int endHour,
        int occurrenceCount,
        bool expected
    )
    {
        // Arrange
        var series = await _service.CreateShiftSeriesAsync(
            CreateShiftSeriesRequest(recurrenceRule: $"FREQ=DAILY;COUNT={occurrenceCount}") with
            {
                TimeZoneId = "UTC",
                StartAtUtc = new DateTimeOffset(2026, 6, startDay, startHour, 0, 0, TimeSpan.Zero),
                EndAtUtc = new DateTimeOffset(2026, 6, endDay, endHour, 0, 0, TimeSpan.Zero),
            },
            TestContext.Current.CancellationToken
        );
        var materializedEntries = await _dbContext
            .ShiftEntries.Include(entry => entry.Event)
            .Where(entry => entry.ShiftSeriesId == series.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        _dbContext.ShiftEntries.RemoveRange(materializedEntries);
        _dbContext.Events.RemoveRange(materializedEntries.Select(entry => entry.Event));
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var results = await _service.GetShiftSeriesAsync(
            new ShiftSeriesQueryParams
            {
                LocationId = 5,
                StartAtUtc = new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero),
                EndAtUtc = new DateTimeOffset(2026, 6, 3, 0, 0, 0, TimeSpan.Zero),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(expected, results.Any(result => result.Id == series.Id));
    }

    [Fact]
    public async Task GetShiftSeriesAsync_WhenMultipleSeriesExist_ReturnsGeneratedIdsForEachSeries()
    {
        // Arrange
        var first = await _service.CreateShiftSeriesAsync(
            CreateShiftSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=2", userIds: [UserA]),
            TestContext.Current.CancellationToken
        );
        var second = await _service.CreateShiftSeriesAsync(
            CreateShiftSeriesRequest(title: "Other", recurrenceRule: "FREQ=DAILY;COUNT=3", userIds: [UserB]) with
            {
                LocationId = 9,
            },
            TestContext.Current.CancellationToken
        );

        // Act
        var result = await _service.GetShiftSeriesAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var firstResult = result.Single(x => x.Id == first.Id);
        Assert.Equal("Series", firstResult.Title);
        Assert.Equal("Description", firstResult.Description);
        Assert.Equal("Notes", firstResult.Notes);
        Assert.Equal("blue", firstResult.Color);
        Assert.Equal("FREQ=DAILY;COUNT=2", firstResult.RecurrenceRule);
        Assert.Equal("America/Vancouver", firstResult.TimeZoneId);
        Assert.Equal(new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero), firstResult.StartAtUtc);
        Assert.Equal(new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero), firstResult.EndAtUtc);
        Assert.False(firstResult.AllDay);
        Assert.Equal("draft", firstResult.StatusTypeCode);
        Assert.Equal(5, firstResult.LocationId);
        Assert.Equal(2, firstResult.EventIds.Count);
        Assert.Equal(2, firstResult.ShiftEntryIds.Count);
        var secondResult = result.Single(x => x.Id == second.Id);
        Assert.Equal(3, secondResult.EventIds.Count);
        Assert.Equal(3, secondResult.ShiftEntryIds.Count);
    }

    [Fact]
    public void ShiftSeriesResponse_DoesNotExposeFullEntryPayloads()
    {
        // Assert
        Assert.Null(typeof(ShiftSeriesResponse).GetProperty("Events"));
        Assert.Null(typeof(ShiftSeriesResponse).GetProperty("ShiftEntries"));
        Assert.NotNull(typeof(ShiftSeriesResponse).GetProperty("EventIds"));
        Assert.NotNull(typeof(ShiftSeriesResponse).GetProperty("ShiftEntryIds"));
    }

    [Fact]
    public async Task GetShiftSeriesByIdAsync_WhenFound_ReturnsEventSeriesFieldsAndShiftFields()
    {
        // Arrange
        var created = await _service.CreateShiftSeriesAsync(
            CreateShiftSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=2", userIds: [UserA, UserB]),
            TestContext.Current.CancellationToken
        );
        _dbContext.ChangeTracker.Clear();

        // Act
        var result = await _service.GetShiftSeriesByIdAsync(created.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
        Assert.Equal(created.EventSeriesId, result.EventSeriesId);
        Assert.Equal("Series", result.Title);
        Assert.Equal("Description", result.Description);
        Assert.Equal("Notes", result.Notes);
        Assert.Equal("blue", result.Color);
        Assert.Equal("FREQ=DAILY;COUNT=2", result.RecurrenceRule);
        Assert.Equal("America/Vancouver", result.TimeZoneId);
        Assert.Equal(new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero), result.StartAtUtc);
        Assert.Equal(new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero), result.EndAtUtc);
        Assert.False(result.AllDay);
        Assert.Equal(SchedulingConstants.ShiftEventTypeCode, result.EventTypeCode);
        Assert.Equal("draft", result.StatusTypeCode);
        Assert.Null(result.CancelledAt);
        Assert.Null(result.CancelledByUserId);
        Assert.Null(result.CancellationReason);
        Assert.Equal(5, result.LocationId);
        Assert.Equal([UserA, UserB], result.UserIds);
        Assert.Equal(2, result.EventIds.Count);
        Assert.Equal(2, result.ShiftEntryIds.Count);
    }

    [Fact]
    public async Task GetShiftSeriesByIdAsync_WhenMissing_ReturnsNull()
    {
        // Act
        var result = await _service.GetShiftSeriesByIdAsync(999, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateShiftSeriesAsync_WhenOnlyUsersChange_DoesNotRegenerateEvents()
    {
        // Arrange
        var created = await _service.CreateShiftSeriesAsync(
            CreateShiftSeriesRequest(userIds: [UserA, UserB], recurrenceRule: "FREQ=DAILY;COUNT=2"),
            TestContext.Current.CancellationToken
        );
        var originalEventIds = created.EventIds.Order().ToArray();
        var originalShiftEntryIds = created.ShiftEntryIds.Order().ToArray();
        var request = CreateShiftSeriesRequest(userIds: [UserB, UserC], recurrenceRule: "FREQ=DAILY;COUNT=2");

        // Act
        var result = await _service.UpdateShiftSeriesAsync(created.Id, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal([UserB, UserC], result.UserIds.Order().ToArray());
        Assert.Equal(originalEventIds, result.EventIds.Order().ToArray());
        Assert.Equal(originalShiftEntryIds, result.ShiftEntryIds.Order().ToArray());

        var entity = await _dbContext
            .ShiftSeries.Include(x => x.EventSeries)
            .Include(x => x.Users)
            .SingleAsync(x => x.Id == created.Id, TestContext.Current.CancellationToken);
        Assert.Equal([UserB, UserC], entity.Users.Select(x => x.UserId).Order().ToArray());
        Assert.DoesNotContain(_dbContext.ShiftSeriesUsers, x => x.ShiftSeriesId == created.Id && x.UserId == UserA);
    }

    [Fact]
    public async Task UpdateShiftSeriesAsync_WhenOnlyUsersChange_UpdatesNonOverriddenChildUsers()
    {
        // Arrange
        var created = await _service.CreateShiftSeriesAsync(
            CreateShiftSeriesRequest(userIds: [UserA, UserB], recurrenceRule: "FREQ=DAILY;COUNT=2"),
            TestContext.Current.CancellationToken
        );

        // Act
        await _service.UpdateShiftSeriesAsync(
            created.Id,
            CreateShiftSeriesRequest(userIds: [UserB, UserC], recurrenceRule: "FREQ=DAILY;COUNT=2"),
            TestContext.Current.CancellationToken
        );

        // Assert
        var entries = await _dbContext
            .ShiftEntries.Include(entry => entry.Users)
            .Where(entry => entry.ShiftSeriesId == created.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.All(
            entries,
            entry => Assert.Equal([UserB, UserC], entry.Users.Select(user => user.UserId).Order().ToArray())
        );
    }

    [Fact]
    public async Task UpdateShiftSeriesAsync_WhenOnlyUsersChange_PreservesOverriddenChildUsers()
    {
        // Arrange
        var created = await _service.CreateShiftSeriesAsync(
            CreateShiftSeriesRequest(userIds: [UserA, UserB], recurrenceRule: "FREQ=DAILY;COUNT=2"),
            TestContext.Current.CancellationToken
        );
        var overriddenEntry = await _dbContext
            .ShiftEntries.Include(entry => entry.Users)
            .OrderBy(entry => entry.Id)
            .FirstAsync(entry => entry.ShiftSeriesId == created.Id, TestContext.Current.CancellationToken);
        _dbContext.ShiftEntryUsers.RemoveRange(overriddenEntry.Users);
        overriddenEntry.Users = [new ShiftEntryUser { ShiftEntryId = overriddenEntry.Id, UserId = UserC }];
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _service.UpdateShiftSeriesAsync(
            created.Id,
            CreateShiftSeriesRequest(userIds: [UserB, UserC], recurrenceRule: "FREQ=DAILY;COUNT=2"),
            TestContext.Current.CancellationToken
        );

        // Assert
        var entries = await _dbContext
            .ShiftEntries.Include(entry => entry.Users)
            .Where(entry => entry.ShiftSeriesId == created.Id)
            .OrderBy(entry => entry.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal([UserC], entries[0].Users.Select(user => user.UserId).ToArray());
        Assert.Equal([UserB, UserC], entries[1].Users.Select(user => user.UserId).Order().ToArray());
    }

    [Fact]
    public async Task UpdateShiftSeriesAsync_WhenMissing_ReturnsNull()
    {
        // Act
        var result = await _service.UpdateShiftSeriesAsync(
            999,
            CreateShiftSeriesRequest(),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateShiftSeriesAsync_WhenEventSeriesIsNotShift_ThrowsInvalidOperationException()
    {
        // Arrange
        var shiftSeries = await AddShiftSeriesAsync(eventTypeCode: CalendarEventTypeCodes.General);

        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateShiftSeriesAsync(
                shiftSeries.Id,
                CreateShiftSeriesRequest(),
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task PublishShiftSeriesAsync_WhenDraftSeries_PublishesDraftChildrenOnly()
    {
        // Arrange
        var shiftSeries = await AddShiftSeriesAsync(
            statusTypeCode: CalendarEventStatusTypeCodes.Draft,
            cancelledByUserId: CancelledByUser,
            cancellationReason: "old"
        );
        var draftEntry = await AddShiftEntryAsync(
            shiftSeriesId: shiftSeries.Id,
            eventSeriesId: shiftSeries.EventSeriesId,
            statusTypeCode: CalendarEventStatusTypeCodes.Draft,
            cancelledByUserId: CancelledByUser,
            cancellationReason: "old"
        );
        var activeEntry = await AddShiftEntryAsync(
            startAtUtc: new DateTimeOffset(2026, 6, 2, 17, 0, 0, TimeSpan.Zero),
            endAtUtc: new DateTimeOffset(2026, 6, 3, 1, 0, 0, TimeSpan.Zero),
            shiftSeriesId: shiftSeries.Id,
            eventSeriesId: shiftSeries.EventSeriesId,
            statusTypeCode: CalendarEventStatusTypeCodes.Active
        );
        var cancelledEntry = await AddShiftEntryAsync(
            startAtUtc: new DateTimeOffset(2026, 6, 3, 17, 0, 0, TimeSpan.Zero),
            endAtUtc: new DateTimeOffset(2026, 6, 4, 1, 0, 0, TimeSpan.Zero),
            shiftSeriesId: shiftSeries.Id,
            eventSeriesId: shiftSeries.EventSeriesId,
            statusTypeCode: CalendarEventStatusTypeCodes.Cancelled,
            isException: true,
            cancelledByUserId: CancelledByUser,
            cancellationReason: "old exception"
        );

        // Act
        var result = await _service.PublishShiftSeriesAsync(shiftSeries.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        var eventSeries = await _dbContext.EventSeries.SingleAsync(
            x => x.Id == shiftSeries.EventSeriesId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(CalendarEventStatusTypeCodes.Active, eventSeries.StatusTypeCode);

        var childEvents = await _dbContext
            .Events.Where(x =>
                x.Id == draftEntry.EventId || x.Id == activeEntry.EventId || x.Id == cancelledEntry.EventId
            )
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, childEvents.Count);
        var draftEvent = childEvents.Single(x => x.Id == draftEntry.EventId);
        Assert.Equal(CalendarEventStatusTypeCodes.Active, draftEvent.StatusTypeCode);

        var activeEvent = childEvents.Single(x => x.Id == activeEntry.EventId);
        Assert.Equal(CalendarEventStatusTypeCodes.Active, activeEvent.StatusTypeCode);

        var cancelledEvent = childEvents.Single(x => x.Id == cancelledEntry.EventId);
        Assert.Equal(CalendarEventStatusTypeCodes.Cancelled, cancelledEvent.StatusTypeCode);
        Assert.NotNull(cancelledEvent.CancelledAt);
        Assert.Equal(CancelledByUser, cancelledEvent.CancelledByUserId);
        Assert.Equal("old exception", cancelledEvent.CancellationReason);
    }

    [Fact]
    public async Task PublishShiftSeriesAsync_WhenSeriesIsNotDraft_ThrowsInvalidOperationException()
    {
        // Arrange
        var shiftSeries = await AddShiftSeriesAsync(statusTypeCode: CalendarEventStatusTypeCodes.Cancelled);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.PublishShiftSeriesAsync(shiftSeries.Id, TestContext.Current.CancellationToken)
        );

        // Assert
        Assert.Contains("draft status", exception.Message);
    }

    [Fact]
    public async Task PublishShiftSeriesAsync_WhenMissing_ReturnsNull()
    {
        // Act
        var result = await _service.PublishShiftSeriesAsync(999, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ExpireShiftSeriesAsync_WhenFound_SetsCancelledFields()
    {
        // Arrange
        var shiftSeries = await AddShiftSeriesAsync();
        var generatedEntry = await AddShiftEntryAsync(
            shiftSeriesId: shiftSeries.Id,
            eventSeriesId: shiftSeries.EventSeriesId,
            statusTypeCode: CalendarEventStatusTypeCodes.Active
        );
        var exceptionEntry = await AddShiftEntryAsync(
            startAtUtc: new DateTimeOffset(2026, 6, 2, 17, 0, 0, TimeSpan.Zero),
            endAtUtc: new DateTimeOffset(2026, 6, 3, 1, 0, 0, TimeSpan.Zero),
            shiftSeriesId: shiftSeries.Id,
            eventSeriesId: shiftSeries.EventSeriesId,
            statusTypeCode: CalendarEventStatusTypeCodes.Active,
            isException: true
        );

        // Act
        var result = await _service.ExpireShiftSeriesAsync(
            shiftSeries.Id,
            new ExpireShiftRequest { CancellationReason = "  no staff  " },
            CancelledByUser,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result);
        var eventSeries = await _dbContext.EventSeries.SingleAsync(
            x => x.Id == shiftSeries.EventSeriesId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(CalendarEventStatusTypeCodes.Cancelled, eventSeries.StatusTypeCode);
        Assert.NotNull(eventSeries.CancelledAt);
        Assert.Equal(CancelledByUser, eventSeries.CancelledByUserId);
        Assert.Equal("no staff", eventSeries.CancellationReason);

        var childEvents = await _dbContext
            .Events.Where(x => x.Id == generatedEntry.EventId || x.Id == exceptionEntry.EventId)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, childEvents.Count);
        Assert.All(
            childEvents,
            childEvent =>
            {
                Assert.Equal(CalendarEventStatusTypeCodes.Cancelled, childEvent.StatusTypeCode);
                Assert.NotNull(childEvent.CancelledAt);
                Assert.Equal(CancelledByUser, childEvent.CancelledByUserId);
                Assert.Equal("no staff", childEvent.CancellationReason);
            }
        );
    }

    [Fact]
    public async Task ExpireShiftSeriesAsync_WhenMissing_ReturnsNull()
    {
        // Act
        var result = await _service.ExpireShiftSeriesAsync(
            999,
            new ExpireShiftRequest(),
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteShiftSeriesAsync_WhenDraftGraph_DeletesSeriesEntriesUsersEventsAndEventSeries()
    {
        // Arrange
        var shiftSeries = await AddShiftSeriesAsync();
        var firstEntry = await AddShiftEntryAsync(
            shiftSeriesId: shiftSeries.Id,
            eventSeriesId: shiftSeries.EventSeriesId,
            userIds: [UserA, UserB]
        );
        var secondEntry = await AddShiftEntryAsync(
            startAtUtc: new DateTimeOffset(2026, 6, 2, 16, 0, 0, TimeSpan.Zero),
            endAtUtc: new DateTimeOffset(2026, 6, 3, 0, 0, 0, TimeSpan.Zero),
            shiftSeriesId: shiftSeries.Id,
            eventSeriesId: shiftSeries.EventSeriesId,
            userIds: [UserC]
        );

        // Act
        var result = await _service.DeleteShiftSeriesAsync(shiftSeries.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
        Assert.False(
            await _dbContext.ShiftSeries.AnyAsync(x => x.Id == shiftSeries.Id, TestContext.Current.CancellationToken)
        );
        Assert.False(
            await _dbContext.EventSeries.AnyAsync(
                x => x.Id == shiftSeries.EventSeriesId,
                TestContext.Current.CancellationToken
            )
        );
        Assert.False(
            await _dbContext.ShiftEntries.AnyAsync(
                x => x.Id == firstEntry.Id || x.Id == secondEntry.Id,
                TestContext.Current.CancellationToken
            )
        );
        Assert.False(
            await _dbContext.Events.AnyAsync(
                x => x.Id == firstEntry.EventId || x.Id == secondEntry.EventId,
                TestContext.Current.CancellationToken
            )
        );
        Assert.Empty(_dbContext.ShiftSeriesUsers);
        Assert.Empty(_dbContext.ShiftEntryUsers);
    }

    [Fact]
    public async Task DeleteShiftSeriesAsync_WhenChildEventsAreNotDraft_DeletesDraftChildrenAndKeepsNonDraftChildren()
    {
        // Arrange
        var shiftSeries = await AddShiftSeriesAsync();
        var draftEntry = await AddShiftEntryAsync(
            shiftSeriesId: shiftSeries.Id,
            eventSeriesId: shiftSeries.EventSeriesId,
            statusTypeCode: CalendarEventStatusTypeCodes.Draft
        );
        var activeEntry = await AddShiftEntryAsync(
            startAtUtc: new DateTimeOffset(2026, 6, 2, 16, 0, 0, TimeSpan.Zero),
            endAtUtc: new DateTimeOffset(2026, 6, 3, 0, 0, 0, TimeSpan.Zero),
            shiftSeriesId: shiftSeries.Id,
            eventSeriesId: shiftSeries.EventSeriesId,
            statusTypeCode: CalendarEventStatusTypeCodes.Active
        );
        var cancelledEntry = await AddShiftEntryAsync(
            startAtUtc: new DateTimeOffset(2026, 6, 3, 16, 0, 0, TimeSpan.Zero),
            endAtUtc: new DateTimeOffset(2026, 6, 4, 0, 0, 0, TimeSpan.Zero),
            shiftSeriesId: shiftSeries.Id,
            eventSeriesId: shiftSeries.EventSeriesId,
            statusTypeCode: CalendarEventStatusTypeCodes.Cancelled
        );

        // Act
        var result = await _service.DeleteShiftSeriesAsync(shiftSeries.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
        Assert.False(
            await _dbContext.ShiftSeries.AnyAsync(x => x.Id == shiftSeries.Id, TestContext.Current.CancellationToken)
        );
        Assert.False(
            await _dbContext.EventSeries.AnyAsync(
                x => x.Id == shiftSeries.EventSeriesId,
                TestContext.Current.CancellationToken
            )
        );
        Assert.False(
            await _dbContext.ShiftEntries.AnyAsync(x => x.Id == draftEntry.Id, TestContext.Current.CancellationToken)
        );
        Assert.False(
            await _dbContext.Events.AnyAsync(x => x.Id == draftEntry.EventId, TestContext.Current.CancellationToken)
        );

        var retainedEntries = await _dbContext
            .ShiftEntries.Include(x => x.Event)
            .Where(x => x.Id == activeEntry.Id || x.Id == cancelledEntry.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, retainedEntries.Count);
        Assert.All(retainedEntries, entry => Assert.Null(entry.ShiftSeriesId));
        Assert.Contains(
            retainedEntries,
            entry =>
                entry.EventId == activeEntry.EventId
                && entry.Event!.StatusTypeCode == CalendarEventStatusTypeCodes.Active
                && entry.Event.EventSeriesId is null
        );
        Assert.Contains(
            retainedEntries,
            entry =>
                entry.EventId == cancelledEntry.EventId
                && entry.Event!.StatusTypeCode == CalendarEventStatusTypeCodes.Cancelled
                && entry.Event.EventSeriesId is null
        );
    }

    [Fact]
    public async Task DeleteShiftSeriesAsync_WhenSeriesIsNotDraft_ThrowsInvalidOperationExceptionAndKeepsGraph()
    {
        // Arrange
        var shiftSeries = await AddShiftSeriesAsync(statusTypeCode: CalendarEventStatusTypeCodes.Active);
        var entry = await AddShiftEntryAsync(
            shiftSeriesId: shiftSeries.Id,
            eventSeriesId: shiftSeries.EventSeriesId,
            statusTypeCode: CalendarEventStatusTypeCodes.Draft
        );

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.DeleteShiftSeriesAsync(shiftSeries.Id, TestContext.Current.CancellationToken)
        );

        // Assert
        Assert.Contains("draft status", exception.Message);
        Assert.True(
            await _dbContext.ShiftSeries.AnyAsync(x => x.Id == shiftSeries.Id, TestContext.Current.CancellationToken)
        );
        Assert.True(
            await _dbContext.ShiftEntries.AnyAsync(x => x.Id == entry.Id, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task DeleteShiftSeriesAsync_WhenMissing_ReturnsFalse()
    {
        // Act
        var result = await _service.DeleteShiftSeriesAsync(999, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CreateShiftEntryAsync_WhenSeriesProvided_AllowsUsersDifferentFromSeriesAndLinksEventSeries()
    {
        // Arrange
        var series = await _service.CreateShiftSeriesAsync(
            CreateShiftSeriesRequest(userIds: [UserA, UserB]),
            TestContext.Current.CancellationToken
        );
        var request = CreateShiftEntryRequest(
            shiftSeriesId: series.Id,
            userIds: [UserA, UserC],
            startAtUtc: new DateTimeOffset(2026, 6, 2, 16, 0, 0, TimeSpan.Zero),
            endAtUtc: new DateTimeOffset(2026, 6, 2, 23, 0, 0, TimeSpan.Zero)
        );

        // Act
        var result = await _service.CreateShiftEntryAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(series.Id, result.ShiftSeriesId);
        Assert.Equal([UserA, UserC], result.UserIds);

        var entity = await _dbContext
            .ShiftEntries.Include(x => x.Event)
            .Include(x => x.Users)
            .SingleAsync(x => x.Id == result.Id, TestContext.Current.CancellationToken);
        Assert.Equal(series.EventSeriesId, entity.Event!.EventSeriesId);
        Assert.Equal([UserA, UserC], entity.Users.Select(x => x.UserId).Order().ToArray());
    }

    [Fact]
    public async Task CreateShiftEntryAsync_WhenSeriesIdMissing_ThrowsKeyNotFoundException()
    {
        // Act / Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.CreateShiftEntryAsync(
                CreateShiftEntryRequest(shiftSeriesId: 999),
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task CreateShiftEntryAsync_WhenExactLocationAndTimeExist_AllowsDuplicate()
    {
        // Arrange
        var request = CreateShiftEntryRequest();
        await _service.CreateShiftEntryAsync(request, TestContext.Current.CancellationToken);

        // Act
        var duplicate = await _service.CreateShiftEntryAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(0, duplicate.Id);
        Assert.Equal(2, await _dbContext.ShiftEntries.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateShiftEntryAsync_WhenSameTimeAtDifferentLocation_CreatesShiftEntry()
    {
        // Arrange
        var request = CreateShiftEntryRequest();
        await _service.CreateShiftEntryAsync(request, TestContext.Current.CancellationToken);

        // Act
        var result = await _service.CreateShiftEntryAsync(
            request with
            {
                LocationId = 9,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(9, result.LocationId);
    }

    [Fact]
    public async Task CreateShiftEntryAsync_WhenSameLocationAtDifferentTime_CreatesShiftEntry()
    {
        // Arrange
        var request = CreateShiftEntryRequest();
        await _service.CreateShiftEntryAsync(request, TestContext.Current.CancellationToken);

        // Act
        var result = await _service.CreateShiftEntryAsync(
            request with
            {
                StartAtUtc = request.StartAtUtc.AddHours(1),
                EndAtUtc = request.EndAtUtc.Value.AddHours(1),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(request.StartAtUtc.AddHours(1), result.StartAtUtc);
    }

    [Fact]
    public async Task GetShiftEntriesAsync_WhenFiltersProvided_MatchesChildUserSeriesAndEvent()
    {
        // Arrange
        var series = await _service.CreateShiftSeriesAsync(
            CreateShiftSeriesRequest(userIds: [UserA]),
            TestContext.Current.CancellationToken
        );
        var first = await _service.CreateShiftEntryAsync(
            CreateShiftEntryRequest(
                shiftSeriesId: series.Id,
                userIds: [UserA, UserB],
                startAtUtc: new DateTimeOffset(2026, 6, 2, 16, 0, 0, TimeSpan.Zero),
                endAtUtc: new DateTimeOffset(2026, 6, 2, 23, 0, 0, TimeSpan.Zero)
            ),
            TestContext.Current.CancellationToken
        );
        await _service.CreateShiftEntryAsync(
            CreateShiftEntryRequest(
                shiftSeriesId: null,
                title: "Other",
                userIds: [UserC],
                startAtUtc: new DateTimeOffset(2026, 6, 3, 16, 0, 0, TimeSpan.Zero),
                endAtUtc: new DateTimeOffset(2026, 6, 3, 23, 0, 0, TimeSpan.Zero)
            ),
            TestContext.Current.CancellationToken
        );

        // Act
        var result = await _service.GetShiftEntriesAsync(
            new ShiftEntryQueryParams
            {
                ShiftSeriesId = series.Id,
                EventId = first.EventId,
                UserId = UserB,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var item = Assert.Single(result);
        Assert.Equal(first.Id, item.Id);
        Assert.Equal([UserA, UserB], item.UserIds);
    }

    [Fact]
    public async Task GetShiftEntriesAsync_WhenLocationAndDateRangeProvided_ReturnsOnlyOverlappingEntries()
    {
        // Arrange
        var matching = await _service.CreateShiftEntryAsync(
            CreateShiftEntryRequest(),
            TestContext.Current.CancellationToken
        );
        await _service.CreateShiftEntryAsync(
            CreateShiftEntryRequest(title: "Wrong location") with
            {
                LocationId = 9,
            },
            TestContext.Current.CancellationToken
        );
        await _service.CreateShiftEntryAsync(
            CreateShiftEntryRequest(
                title: "Outside range",
                startAtUtc: new DateTimeOffset(2026, 6, 2, 16, 0, 0, TimeSpan.Zero),
                endAtUtc: new DateTimeOffset(2026, 6, 2, 23, 0, 0, TimeSpan.Zero)
            ),
            TestContext.Current.CancellationToken
        );
        var cancelled = await _service.CreateShiftEntryAsync(
            CreateShiftEntryRequest(
                title: "Cancelled",
                startAtUtc: new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero),
                endAtUtc: new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero)
            ),
            TestContext.Current.CancellationToken
        );
        var cancelledEvent = await _dbContext.Events.SingleAsync(
            eventEntity => eventEntity.Id == cancelled.EventId,
            TestContext.Current.CancellationToken
        );
        cancelledEvent.StatusTypeCode = CalendarEventStatusTypeCodes.Cancelled;
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _service.GetShiftEntriesAsync(
            new ShiftEntryQueryParams
            {
                LocationId = 5,
                StartAtUtc = new DateTimeOffset(2026, 6, 1, 7, 0, 0, TimeSpan.Zero),
                EndAtUtc = new DateTimeOffset(2026, 6, 2, 7, 0, 0, TimeSpan.Zero),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var item = Assert.Single(result);
        Assert.Equal(matching.Id, item.Id);
    }

    [Fact]
    public async Task GetShiftEntriesAsync_WhenEndIsNull_ReturnsOnlyActivePointOnRequestedDayAndLocation()
    {
        // Arrange
        var matching = await _service.CreateShiftEntryAsync(
            CreateShiftEntryRequest() with
            {
                EndAtUtc = null,
            },
            TestContext.Current.CancellationToken
        );
        await _service.CreateShiftEntryAsync(
            CreateShiftEntryRequest() with
            {
                EndAtUtc = null,
                StartAtUtc = new DateTimeOffset(2026, 6, 2, 16, 0, 0, TimeSpan.Zero),
            },
            TestContext.Current.CancellationToken
        );
        await _service.CreateShiftEntryAsync(
            CreateShiftEntryRequest() with
            {
                EndAtUtc = null,
                LocationId = 9,
            },
            TestContext.Current.CancellationToken
        );
        var cancelled = await _service.CreateShiftEntryAsync(
            CreateShiftEntryRequest(title: "Cancelled") with
            {
                EndAtUtc = null,
                StartAtUtc = new DateTimeOffset(2026, 6, 1, 18, 0, 0, TimeSpan.Zero),
            },
            TestContext.Current.CancellationToken
        );
        var cancelledEvent = await _dbContext.Events.SingleAsync(
            eventEntity => eventEntity.Id == cancelled.EventId,
            TestContext.Current.CancellationToken
        );
        cancelledEvent.StatusTypeCode = CalendarEventStatusTypeCodes.Cancelled;
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var results = await _service.GetShiftEntriesAsync(
            new ShiftEntryQueryParams
            {
                LocationId = 5,
                StartAtUtc = new DateTimeOffset(2026, 6, 1, 7, 0, 0, TimeSpan.Zero),
                EndAtUtc = new DateTimeOffset(2026, 6, 2, 7, 0, 0, TimeSpan.Zero),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var result = Assert.Single(results);
        Assert.Equal(matching.Id, result.Id);
        Assert.Null(result.EndAtUtc);
    }

    [Fact]
    public async Task GetShiftEntryByIdAsync_WhenMissing_ReturnsNull()
    {
        // Act
        var result = await _service.GetShiftEntryByIdAsync(999, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateShiftEntryAsync_WhenUsersAndSeriesChange_PersistsExactUsersAndFields()
    {
        // Arrange
        var originalSeries = await _service.CreateShiftSeriesAsync(
            CreateShiftSeriesRequest(userIds: [UserA]),
            TestContext.Current.CancellationToken
        );
        var newSeries = await _service.CreateShiftSeriesAsync(
            CreateShiftSeriesRequest(title: "New series", userIds: [UserC]) with
            {
                LocationId = 9,
            },
            TestContext.Current.CancellationToken
        );
        var entry = await _service.CreateShiftEntryAsync(
            CreateShiftEntryRequest(
                shiftSeriesId: originalSeries.Id,
                userIds: [UserA, UserB],
                startAtUtc: new DateTimeOffset(2026, 6, 2, 16, 0, 0, TimeSpan.Zero),
                endAtUtc: new DateTimeOffset(2026, 6, 2, 23, 0, 0, TimeSpan.Zero)
            ),
            TestContext.Current.CancellationToken
        );
        var request = CreateShiftEntryRequest(
            shiftSeriesId: newSeries.Id,
            title: "Updated entry",
            userIds: [UserB, UserC]
        ) with
        {
            LocationId = 9,
            StartAtUtc = new DateTimeOffset(2026, 6, 2, 16, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2026, 6, 2, 23, 0, 0, TimeSpan.Zero),
        };

        // Act
        var result = await _service.UpdateShiftEntryAsync(entry.Id, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(newSeries.Id, result.ShiftSeriesId);
        Assert.Equal([UserB, UserC], result.UserIds.Order().ToArray());

        var entity = await _dbContext
            .ShiftEntries.Include(x => x.Event)
            .Include(x => x.Users)
            .SingleAsync(x => x.Id == entry.Id, TestContext.Current.CancellationToken);
        Assert.Equal("Updated entry", entity.Event!.Title);
        Assert.Equal(newSeries.EventSeriesId, entity.Event.EventSeriesId);
        Assert.Equal([UserB, UserC], entity.Users.Select(x => x.UserId).Order().ToArray());
    }

    [Fact]
    public async Task UpdateShiftEntryAsync_WhenLocationAndTimeAreUnchanged_AllowsSelfUpdate()
    {
        // Arrange
        var request = CreateShiftEntryRequest();
        var entry = await _service.CreateShiftEntryAsync(request, TestContext.Current.CancellationToken);

        // Act
        var result = await _service.UpdateShiftEntryAsync(entry.Id, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(entry.Id, result.Id);
    }

    [Fact]
    public async Task UpdateShiftEntryAsync_WhenExactLocationAndTimeExist_AllowsDuplicate()
    {
        // Arrange
        var existingRequest = CreateShiftEntryRequest();
        await _service.CreateShiftEntryAsync(existingRequest, TestContext.Current.CancellationToken);
        var entry = await _service.CreateShiftEntryAsync(
            existingRequest with
            {
                StartAtUtc = existingRequest.StartAtUtc.AddDays(1),
                EndAtUtc = existingRequest.EndAtUtc.Value.AddDays(1),
            },
            TestContext.Current.CancellationToken
        );

        // Act
        var result = await _service.UpdateShiftEntryAsync(
            entry.Id,
            existingRequest,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(existingRequest.StartAtUtc, result.StartAtUtc);
    }

    [Fact]
    public async Task UpdateShiftEntryAsync_WhenSeriesOccurrenceTimeChanges_PreservesSeriesTimesAndMarksException()
    {
        // Arrange
        var series = await _service.CreateShiftSeriesAsync(
            CreateShiftSeriesRequest(userIds: [UserA]),
            TestContext.Current.CancellationToken
        );
        var entry = await _dbContext
            .ShiftEntries.Include(x => x.Event)
            .SingleAsync(x => x.ShiftSeriesId == series.Id, TestContext.Current.CancellationToken);
        var originalSeriesStart = entry.Event!.SeriesStartAtUtc;
        var originalSeriesEnd = entry.Event.SeriesEndAtUtc;

        // Act
        var result = await _service.UpdateShiftEntryAsync(
            entry.Id,
            CreateShiftEntryRequest(
                shiftSeriesId: series.Id,
                startAtUtc: entry.Event.StartAtUtc.AddHours(1),
                endAtUtc: entry.Event.EndAtUtc!.Value.AddHours(1)
            ),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result);
        var updatedEvent = await _dbContext.Events.SingleAsync(
            x => x.Id == entry.EventId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(originalSeriesStart, updatedEvent.SeriesStartAtUtc);
        Assert.Equal(originalSeriesEnd, updatedEvent.SeriesEndAtUtc);
        Assert.True(updatedEvent.IsException);
    }

    [Theory]
    [InlineData(CalendarEventStatusTypeCodes.Active)]
    [InlineData(CalendarEventStatusTypeCodes.Cancelled)]
    public async Task UpdateShiftEntryAsync_WhenEntryIsNotDraft_ThrowsAndDoesNotMutate(string statusTypeCode)
    {
        // Arrange
        var entry = await AddShiftEntryAsync(title: "Original", userIds: [UserA], statusTypeCode: statusTypeCode);

        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateShiftEntryAsync(
                entry.Id,
                CreateShiftEntryRequest(title: "Updated", userIds: [UserB]),
                TestContext.Current.CancellationToken
            )
        );

        var unchanged = await _dbContext
            .ShiftEntries.Include(shiftEntry => shiftEntry.Event)
            .Include(shiftEntry => shiftEntry.Users)
            .SingleAsync(shiftEntry => shiftEntry.Id == entry.Id, TestContext.Current.CancellationToken);
        Assert.Equal("Original", unchanged.Event!.Title);
        Assert.Equal([UserA], unchanged.Users.Select(user => user.UserId).ToArray());
    }

    [Fact]
    public async Task UpdateShiftEntryAsync_WhenMissing_ReturnsNull()
    {
        // Act
        var result = await _service.UpdateShiftEntryAsync(
            999,
            CreateShiftEntryRequest(),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData(CalendarEventTypeCodes.General, SchedulingConstants.SourceModule)]
    [InlineData(SchedulingConstants.ShiftEventTypeCode, "calendar")]
    public async Task UpdateShiftEntryAsync_WhenEventIsNotShiftOrSchedulingOwned_ThrowsInvalidOperationException(
        string eventTypeCode,
        string sourceModule
    )
    {
        // Arrange
        var entry = await AddShiftEntryAsync(eventTypeCode: eventTypeCode, sourceModule: sourceModule);

        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateShiftEntryAsync(
                entry.Id,
                CreateShiftEntryRequest(shiftSeriesId: null),
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task PublishShiftEntryAsync_WhenDraft_SetsActive()
    {
        // Arrange
        var entry = await AddShiftEntryAsync(
            statusTypeCode: CalendarEventStatusTypeCodes.Draft,
            cancelledByUserId: CancelledByUser,
            cancellationReason: "old"
        );

        // Act
        var result = await _service.PublishShiftEntryAsync(entry.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        var eventEntity = await _dbContext.Events.SingleAsync(
            x => x.Id == entry.EventId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(CalendarEventStatusTypeCodes.Active, eventEntity.StatusTypeCode);
    }

    [Fact]
    public async Task PublishShiftEntryAsync_WhenEntryIsNotDraft_ThrowsInvalidOperationException()
    {
        // Arrange
        var entry = await AddShiftEntryAsync(statusTypeCode: CalendarEventStatusTypeCodes.Cancelled);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.PublishShiftEntryAsync(entry.Id, TestContext.Current.CancellationToken)
        );

        // Assert
        Assert.Contains("draft status", exception.Message);
    }

    [Fact]
    public async Task PublishShiftEntryAsync_WhenMissing_ReturnsNull()
    {
        // Act
        var result = await _service.PublishShiftEntryAsync(999, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ExpireShiftEntryAsync_WhenFound_SetsCancelledFields()
    {
        // Arrange
        var entry = await AddShiftEntryAsync();

        // Act
        var result = await _service.ExpireShiftEntryAsync(
            entry.Id,
            new ExpireShiftRequest { CancellationReason = "  removed  " },
            CancelledByUser,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result);
        var eventEntity = await _dbContext.Events.SingleAsync(
            x => x.Id == entry.EventId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(CalendarEventStatusTypeCodes.Cancelled, eventEntity.StatusTypeCode);
        Assert.NotNull(eventEntity.CancelledAt);
        Assert.Equal(CancelledByUser, eventEntity.CancelledByUserId);
        Assert.Equal("removed", eventEntity.CancellationReason);
    }

    [Fact]
    public async Task ExpireShiftEntryAsync_WhenMissing_ReturnsNull()
    {
        // Act
        var result = await _service.ExpireShiftEntryAsync(
            999,
            new ExpireShiftRequest(),
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteShiftEntryAsync_WhenDraft_DeletesEntryUsersEntryAndEvent()
    {
        // Arrange
        var entry = await AddShiftEntryAsync(userIds: [UserA, UserB]);

        // Act
        var result = await _service.DeleteShiftEntryAsync(entry.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
        Assert.False(
            await _dbContext.ShiftEntries.AnyAsync(x => x.Id == entry.Id, TestContext.Current.CancellationToken)
        );
        Assert.False(
            await _dbContext.Events.AnyAsync(x => x.Id == entry.EventId, TestContext.Current.CancellationToken)
        );
        Assert.Empty(_dbContext.ShiftEntryUsers);
    }

    [Fact]
    public async Task DeleteShiftEntryAsync_WhenEventIsActive_ThrowsInvalidOperationExceptionAndKeepsEntry()
    {
        // Arrange
        var entry = await AddShiftEntryAsync(statusTypeCode: CalendarEventStatusTypeCodes.Active);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.DeleteShiftEntryAsync(entry.Id, TestContext.Current.CancellationToken)
        );

        // Assert
        Assert.Contains("draft status", exception.Message);
        Assert.True(
            await _dbContext.ShiftEntries.AnyAsync(x => x.Id == entry.Id, TestContext.Current.CancellationToken)
        );
        Assert.True(
            await _dbContext.Events.AnyAsync(x => x.Id == entry.EventId, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task DeleteShiftEntryAsync_WhenMissing_ReturnsFalse()
    {
        // Act
        var result = await _service.DeleteShiftEntryAsync(999, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);
    }

    private async Task SeedBaseDataAsync()
    {
        _dbContext.EventTypes.AddRange(
            CreateEventType(SchedulingConstants.ShiftEventTypeCode),
            CreateEventType(CalendarEventTypeCodes.General)
        );
        _dbContext.EventStatusTypes.AddRange(
            CreateStatusType(CalendarEventStatusTypeCodes.Draft),
            CreateStatusType(CalendarEventStatusTypeCodes.Active),
            CreateStatusType(CalendarEventStatusTypeCodes.Cancelled)
        );
        _dbContext.Locations.AddRange(
            new Location
            {
                Id = 5,
                AgencyId = "A5",
                Name = "Location 5",
                Timezone = "America/Vancouver",
            },
            new Location
            {
                Id = 9,
                AgencyId = "A9",
                Name = "Location 9",
                Timezone = "America/Vancouver",
            }
        );
        _dbContext.Users.AddRange(
            CreateUser(UserA, "UserA"),
            CreateUser(UserB, "UserB"),
            CreateUser(UserC, "UserC"),
            CreateUser(CancelledByUser, "CancelUser")
        );

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<ShiftSeries> AddShiftSeriesAsync(
        string eventTypeCode = SchedulingConstants.ShiftEventTypeCode,
        string statusTypeCode = CalendarEventStatusTypeCodes.Draft,
        Guid? cancelledByUserId = null,
        string? cancellationReason = null
    )
    {
        var eventSeries = new EventSeries
        {
            Title = "Seeded series",
            StartAtUtc = new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
            EventTypeCode = eventTypeCode,
            StatusTypeCode = statusTypeCode,
            CancelledAt = cancelledByUserId.HasValue ? DateTimeOffset.UtcNow : null,
            CancelledByUserId = cancelledByUserId,
            CancellationReason = cancellationReason,
        };
        var shiftSeries = new ShiftSeries
        {
            EventSeries = eventSeries,
            Users = [new ShiftSeriesUser { UserId = UserA }],
        };

        _dbContext.ShiftSeries.Add(shiftSeries);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return shiftSeries;
    }

    private async Task<ShiftEntry> AddShiftEntryAsync(
        string title = "Seeded entry",
        DateTimeOffset? startAtUtc = null,
        DateTimeOffset? endAtUtc = null,
        bool openEnded = false,
        int? shiftSeriesId = null,
        int? eventSeriesId = null,
        int? locationId = null,
        IReadOnlyCollection<Guid>? userIds = null,
        string eventTypeCode = SchedulingConstants.ShiftEventTypeCode,
        string statusTypeCode = CalendarEventStatusTypeCodes.Draft,
        string sourceModule = SchedulingConstants.SourceModule,
        bool isException = false,
        Guid? cancelledByUserId = null,
        string? cancellationReason = null
    )
    {
        var start = startAtUtc ?? new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero);
        var eventEntity = new Event
        {
            EventSeriesId = eventSeriesId,
            Title = title,
            StartAtUtc = start,
            EndAtUtc = openEnded ? null : endAtUtc ?? start.AddHours(8),
            EventTypeCode = eventTypeCode,
            StatusTypeCode = statusTypeCode,
            SourceModule = sourceModule,
            IsException = isException,
            SeriesStartAtUtc = eventSeriesId.HasValue ? start : null,
            SeriesEndAtUtc = eventSeriesId.HasValue && !openEnded ? endAtUtc ?? start.AddHours(8) : null,
            LocationId = locationId,
            CancelledAt = cancelledByUserId.HasValue ? DateTimeOffset.UtcNow : null,
            CancelledByUserId = cancelledByUserId,
            CancellationReason = cancellationReason,
        };
        var shiftEntry = new ShiftEntry
        {
            ShiftSeriesId = shiftSeriesId,
            Event = eventEntity,
            Users = (userIds ?? [UserA]).Select(userId => new ShiftEntryUser { UserId = userId }).ToList(),
        };

        _dbContext.ShiftEntries.Add(shiftEntry);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return shiftEntry;
    }

    private static ShiftSeriesRequest CreateShiftSeriesRequest(
        string title = "Series",
        IReadOnlyCollection<Guid>? userIds = null,
        string recurrenceRule = "FREQ=DAILY;COUNT=1"
    ) =>
        new()
        {
            Title = title,
            Description = " Description ",
            Notes = " Notes ",
            Color = " blue ",
            RecurrenceRule = recurrenceRule,
            TimeZoneId = " America/Vancouver ",
            StartAtUtc = new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
            LocationId = 5,
            UserIds = userIds ?? [UserA],
        };

    private static ShiftEntryRequest CreateShiftEntryRequest(
        int? shiftSeriesId = null,
        string title = "Entry",
        IReadOnlyCollection<Guid>? userIds = null,
        DateTimeOffset? startAtUtc = null,
        DateTimeOffset? endAtUtc = null
    ) =>
        new()
        {
            ShiftSeriesId = shiftSeriesId,
            Title = title,
            Description = " Description ",
            Notes = " Notes ",
            Color = " green ",
            StartAtUtc = startAtUtc ?? new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            EndAtUtc = endAtUtc ?? new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
            SeriesStartAtUtc = new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            SeriesEndAtUtc = new DateTimeOffset(2026, 6, 30, 23, 0, 0, TimeSpan.Zero),
            TimeZoneId = " America/Vancouver ",
            AllDay = false,
            LocationId = 5,
            UserIds = userIds ?? [UserA],
        };

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

    private static CalendarTimeZoneResolver CreateCalendarTimeZoneResolver() =>
        new(
            Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "America/Vancouver" }),
            new TimeZoneService()
        );
}
