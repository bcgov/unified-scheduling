using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Unified.Calendar.Mappings;
using Unified.Calendar.Options;
using Unified.Calendar.Services;
using Unified.Common.Time;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Leave;
using Unified.Db.Models.Lookup;
using Unified.Leave.Constants;
using Unified.Leave.Factories;
using Unified.Leave.Mappings;
using Unified.Leave.Models.Scheduling;
using Unified.Leave.Services;

namespace Unified.Tests.Leave.Services;

public sealed class SchedulingLeaveServiceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Start = new(2026, 6, 1, 16, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = new(2026, 6, 1, 23, 0, 0, TimeSpan.Zero);

    private LeaveTestDatabase _database = null!;
    private SchedulingLeaveService _service = null!;
    private LeaveType _vacation = null!;
    private LeaveType _sick = null!;

    public async ValueTask InitializeAsync()
    {
        _database = await LeaveTestDatabase.CreateAsync();
        var db = _database.Db;

        var effective = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        _vacation = new LeaveType
        {
            Code = "Vacation",
            Description = "Vacation",
            EffectiveDate = effective,
        };
        _sick = new LeaveType
        {
            Code = "Sick",
            Description = "Sick",
            EffectiveDate = effective,
        };
        db.LeaveTypes.AddRange(_vacation, _sick);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var timeZoneService = new TimeZoneService();
        var resolver = new CalendarTimeZoneResolver(
            Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "America/Vancouver" }),
            timeZoneService
        );
        var expander = new IcalNetRecurrenceExpander(timeZoneService, resolver);
        var validator = new IcalNetRecurrenceRuleValidator(expander, timeZoneService, resolver);
        var materialization = new EventSeriesMaterializationService(db, validator, expander);
        var calendarLifecycleService = new CalendarLifecycleService();
        var calendarEventMapper = new CalendarEventMapper();
        var schedulingLeaveEventMapper = new SchedulingLeaveEventMapper(calendarEventMapper);
        var eventFactory = new LeaveEventFactory();

        var entryService = new CalendarEventEntryService<SchedulingLeaveEntryRequest, LeaveEntry>(
            NullLogger<CalendarEventEntryService<SchedulingLeaveEntryRequest, LeaveEntry>>.Instance,
            eventFactory,
            calendarLifecycleService,
            calendarEventMapper
        );
        var seriesService = new CalendarEventSeriesService<SchedulingLeaveSeriesRequest, LeaveSeries>(
            eventFactory,
            calendarEventMapper,
            calendarLifecycleService
        );
        _service = new SchedulingLeaveService(
            new SchedulingLeaveEntryService(
                NullLogger<SchedulingLeaveEntryService>.Instance,
                db,
                schedulingLeaveEventMapper,
                entryService
            ),
            new SchedulingLeaveSeriesService(
                NullLogger<SchedulingLeaveSeriesService>.Instance,
                db,
                schedulingLeaveEventMapper,
                seriesService,
                materialization,
                new LeaveSeriesMaterializationHandler(db)
            )
        );
    }

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    // ---- Entries ----

    [Fact]
    public async Task CreateLeaveEntryAsync_WhenRequestHasDuplicateUsers_PersistsDraftEntryWithDistinctUsers()
    {
        var result = await _service.CreateLeaveEntryAsync(
            CreateEntryRequest(userIds: [LeaveTestDatabase.UserA, LeaveTestDatabase.UserB, LeaveTestDatabase.UserA]),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(_vacation.Id, result.LeaveTypeId);
        var entry = await _database
            .Db.LeaveEntries.Include(x => x.Event)
            .Include(x => x.Users)
            .SingleAsync(x => x.Id == result.Id, TestContext.Current.CancellationToken);
        Assert.Equal(CalendarEventStatusTypeCodes.Draft, entry.Event!.StatusTypeCode);
        Assert.Equal(LeaveConstants.LeaveEventTypeCode, entry.Event.EventTypeCode);
        Assert.Equal(LeaveConstants.SourceModule, entry.Event.SourceModule);
        Assert.Equal("Leave", entry.Event.Title);
        Assert.Equal(
            [LeaveTestDatabase.UserA, LeaveTestDatabase.UserB],
            entry.Users.Select(x => x.UserId).Order().ToArray()
        );
    }

    [Fact]
    public async Task CreateLeaveEntryAsync_WhenLeaveTypeDoesNotExist_Throws()
    {
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            _service.CreateLeaveEntryAsync(CreateEntryRequest(leaveTypeId: 9999), TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task CreateLeaveEntryAsync_WhenLeaveSeriesProvided_AssociatesEntryAndCalendarEvent()
    {
        var series = await _service.CreateLeaveSeriesAsync(
            CreateSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=1"),
            TestContext.Current.CancellationToken
        );

        var result = await _service.CreateLeaveEntryAsync(
            CreateEntryRequest(leaveSeriesId: series.Id) with
            {
                SeriesStartAtUtc = Start,
                SeriesEndAtUtc = End,
            },
            TestContext.Current.CancellationToken
        );

        var entry = await _database
            .Db.LeaveEntries.Include(item => item.Event)
            .SingleAsync(item => item.Id == result.Id, TestContext.Current.CancellationToken);
        Assert.Equal(series.Id, result.LeaveSeriesId);
        Assert.Equal(series.Id, entry.LeaveSeriesId);
        Assert.Equal(series.EventSeriesId, entry.Event!.EventSeriesId);
        Assert.Equal(Start, result.SeriesStartAtUtc);
        Assert.Equal(End, result.SeriesEndAtUtc);
    }

    [Fact]
    public async Task CreateLeaveEntryAsync_WhenLeaveSeriesMissing_ThrowsAndPersistsNothing()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.CreateLeaveEntryAsync(
                CreateEntryRequest(leaveSeriesId: 9999),
                TestContext.Current.CancellationToken
            )
        );

        Assert.Empty(await _database.Db.LeaveEntries.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetLeaveEntryByIdAsync_WhenMissing_ReturnsNull()
    {
        var result = await _service.GetLeaveEntryByIdAsync(12345, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetLeaveEntriesByUserAsync_ReturnsOnlyEntriesAssignedToUser()
    {
        var forA = await _service.CreateLeaveEntryAsync(
            CreateEntryRequest(userIds: [LeaveTestDatabase.UserA]),
            TestContext.Current.CancellationToken
        );
        await _service.CreateLeaveEntryAsync(
            CreateEntryRequest(userIds: [LeaveTestDatabase.UserB]),
            TestContext.Current.CancellationToken
        );

        var result = await _service.GetLeaveEntriesByUserAsync(
            LeaveTestDatabase.UserA,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(forA.Id, Assert.Single(result).Id);
    }

    [Fact]
    public async Task UpdateLeaveEntryAsync_WhenNotFound_ReturnsNull()
    {
        var result = await _service.UpdateLeaveEntryAsync(
            12345,
            CreateEntryRequest(),
            TestContext.Current.CancellationToken
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateLeaveEntryAsync_WhenDraft_UpdatesFieldsLeaveTypeAndSyncsUsers()
    {
        var created = await _service.CreateLeaveEntryAsync(
            CreateEntryRequest(userIds: [LeaveTestDatabase.UserA]),
            TestContext.Current.CancellationToken
        );

        var result = await _service.UpdateLeaveEntryAsync(
            created.Id,
            CreateEntryRequest(title: "  Updated  ", leaveTypeId: _sick.Id, userIds: [LeaveTestDatabase.UserB]),
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(result);
        Assert.Equal("Updated", result.Title);
        Assert.Equal(_sick.Id, result.LeaveTypeId);
        var entry = await _database
            .Db.LeaveEntries.Include(x => x.Users)
            .SingleAsync(x => x.Id == created.Id, TestContext.Current.CancellationToken);
        Assert.Equal(_sick.Id, entry.LeaveTypeId);
        Assert.Equal([LeaveTestDatabase.UserB], entry.Users.Select(x => x.UserId).ToArray());
        Assert.Equal(1, await _database.Db.Set<LeaveEntryUser>().CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateLeaveEntryAsync_WhenLeaveSeriesChanges_UpdatesEntryAndCalendarAssociations()
    {
        var created = await _service.CreateLeaveEntryAsync(CreateEntryRequest(), TestContext.Current.CancellationToken);
        var series = await _service.CreateLeaveSeriesAsync(
            CreateSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=1"),
            TestContext.Current.CancellationToken
        );

        var result = await _service.UpdateLeaveEntryAsync(
            created.Id,
            CreateEntryRequest(leaveSeriesId: series.Id),
            TestContext.Current.CancellationToken
        );

        var entry = await _database
            .Db.LeaveEntries.Include(item => item.Event)
            .SingleAsync(item => item.Id == created.Id, TestContext.Current.CancellationToken);
        Assert.Equal(series.Id, result!.LeaveSeriesId);
        Assert.Equal(series.Id, entry.LeaveSeriesId);
        Assert.Equal(series.EventSeriesId, entry.Event!.EventSeriesId);
    }

    [Fact]
    public async Task UpdateLeaveEntryAsync_WhenNotDraft_Throws()
    {
        var created = await _service.CreateLeaveEntryAsync(CreateEntryRequest(), TestContext.Current.CancellationToken);
        await SetEntryStatusAsync(created.Id, CalendarEventStatusTypeCodes.Active);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateLeaveEntryAsync(
                created.Id,
                CreateEntryRequest(title: "New"),
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task UpdateLeaveEntryAsync_WhenEventIsOwnedByAnotherModule_Throws()
    {
        var created = await _service.CreateLeaveEntryAsync(CreateEntryRequest(), TestContext.Current.CancellationToken);
        var entry = await _database
            .Db.LeaveEntries.Include(x => x.Event)
            .SingleAsync(x => x.Id == created.Id, TestContext.Current.CancellationToken);
        entry.Event!.SourceModule = "scheduling";
        await _database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateLeaveEntryAsync(created.Id, CreateEntryRequest(), TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task DeleteLeaveEntryAsync_WhenNotFound_ReturnsFalse()
    {
        var result = await _service.DeleteLeaveEntryAsync(12345, TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    [Fact]
    public async Task DeleteLeaveEntryAsync_WhenDraft_RemovesEntryUsersAndEvent()
    {
        var created = await _service.CreateLeaveEntryAsync(
            CreateEntryRequest(userIds: [LeaveTestDatabase.UserA, LeaveTestDatabase.UserB]),
            TestContext.Current.CancellationToken
        );

        var result = await _service.DeleteLeaveEntryAsync(created.Id, TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.Empty(await _database.Db.LeaveEntries.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await _database.Db.Set<LeaveEntryUser>().ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await _database.Db.Events.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteLeaveEntryAsync_WhenNotDraft_Throws()
    {
        var created = await _service.CreateLeaveEntryAsync(CreateEntryRequest(), TestContext.Current.CancellationToken);
        await SetEntryStatusAsync(created.Id, CalendarEventStatusTypeCodes.Active);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.DeleteLeaveEntryAsync(created.Id, TestContext.Current.CancellationToken)
        );
        Assert.Single(await _database.Db.LeaveEntries.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteLeaveEntryAsync_WhenDraftEntryBelongsToSeries_DeletesOnlyThatOccurrence()
    {
        var series = await _service.CreateLeaveSeriesAsync(
            CreateSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=3"),
            TestContext.Current.CancellationToken
        );

        var result = await _service.DeleteLeaveEntryAsync(
            series.LeaveEntryIds.First(),
            TestContext.Current.CancellationToken
        );

        Assert.True(result);
        Assert.Equal(2, await _database.Db.LeaveEntries.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await _database.Db.LeaveSeries.CountAsync(TestContext.Current.CancellationToken));
    }

    // ---- Series ----

    [Fact]
    public async Task CreateLeaveSeriesAsync_WhenValid_MaterializesDraftEntriesWithUsersAndLeaveType()
    {
        var result = await _service.CreateLeaveSeriesAsync(
            CreateSeriesRequest(
                recurrenceRule: "FREQ=DAILY;COUNT=3",
                userIds: [LeaveTestDatabase.UserA, LeaveTestDatabase.UserA, LeaveTestDatabase.UserB]
            ),
            TestContext.Current.CancellationToken
        );

        Assert.Equal([LeaveTestDatabase.UserA, LeaveTestDatabase.UserB], result.UserIds.Order().ToArray());
        Assert.Equal(3, result.EventIds.Count);
        Assert.Equal(3, result.LeaveEntryIds.Count);
        Assert.Equal(CalendarEventStatusTypeCodes.Draft, result.StatusTypeCode);

        var entries = await _database
            .Db.LeaveEntries.Include(x => x.Users)
            .Include(x => x.Event)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, entries.Count);
        Assert.All(
            entries,
            entry =>
            {
                Assert.Equal(result.Id, entry.LeaveSeriesId);
                Assert.Equal(_vacation.Id, entry.LeaveTypeId);
                Assert.Equal(LeaveConstants.SourceModule, entry.Event!.SourceModule);
                Assert.Equal(2, entry.Users.Count);
            }
        );
    }

    [Fact]
    public async Task CreateLeaveSeriesAsync_WhenRuleIsUnbounded_ThrowsAndPersistsNothing()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateLeaveSeriesAsync(
                CreateSeriesRequest(recurrenceRule: "FREQ=DAILY"),
                TestContext.Current.CancellationToken
            )
        );

        _database.Db.ChangeTracker.Clear();
        Assert.Empty(await _database.Db.LeaveSeries.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await _database.Db.LeaveEntries.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetLeaveSeriesByIdAsync_WhenMissing_ReturnsNull()
    {
        Assert.Null(await _service.GetLeaveSeriesByIdAsync(12345, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetLeaveSeriesByUserAsync_ReturnsOnlySeriesForUser()
    {
        var forA = await _service.CreateLeaveSeriesAsync(
            CreateSeriesRequest(userIds: [LeaveTestDatabase.UserA]),
            TestContext.Current.CancellationToken
        );
        await _service.CreateLeaveSeriesAsync(
            CreateSeriesRequest(userIds: [LeaveTestDatabase.UserB]),
            TestContext.Current.CancellationToken
        );

        var result = await _service.GetLeaveSeriesByUserAsync(
            LeaveTestDatabase.UserA,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(forA.Id, Assert.Single(result).Id);
    }

    [Fact]
    public async Task UpdateLeaveSeriesAsync_WhenNotFound_ReturnsNull()
    {
        var result = await _service.UpdateLeaveSeriesAsync(
            12345,
            CreateSeriesRequest(),
            TestContext.Current.CancellationToken
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateLeaveSeriesAsync_WhenRecurrenceUnchanged_PropagatesCopiedFieldsLeaveTypeAndUsers()
    {
        var created = await _service.CreateLeaveSeriesAsync(
            CreateSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=2", userIds: [LeaveTestDatabase.UserA]),
            TestContext.Current.CancellationToken
        );
        var entryIds = created.LeaveEntryIds.ToArray();

        var result = await _service.UpdateLeaveSeriesAsync(
            created.Id,
            CreateSeriesRequest(
                title: "Renamed",
                recurrenceRule: "FREQ=DAILY;COUNT=2",
                leaveTypeId: _sick.Id,
                userIds: [LeaveTestDatabase.UserA, LeaveTestDatabase.UserB]
            ),
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(result);
        Assert.Equal("Renamed", result.Title);
        Assert.Equal(_sick.Id, result.LeaveTypeId);
        Assert.Equal(entryIds, result.LeaveEntryIds.Order().ToArray());
        var entries = await _database
            .Db.LeaveEntries.Include(x => x.Event)
            .Include(x => x.Users)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.All(
            entries,
            entry =>
            {
                Assert.Equal("Renamed", entry.Event!.Title);
                Assert.Equal(_sick.Id, entry.LeaveTypeId);
                Assert.Equal(
                    [LeaveTestDatabase.UserA, LeaveTestDatabase.UserB],
                    entry.Users.Select(x => x.UserId).Order().ToArray()
                );
            }
        );
    }

    [Fact]
    public async Task UpdateLeaveSeriesAsync_WhenRecurrenceChanged_RegeneratesDraftEntries()
    {
        var created = await _service.CreateLeaveSeriesAsync(
            CreateSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=3"),
            TestContext.Current.CancellationToken
        );

        var result = await _service.UpdateLeaveSeriesAsync(
            created.Id,
            CreateSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=2"),
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(result);
        Assert.Equal(2, result.EventIds.Count);
        Assert.Equal(2, result.LeaveEntryIds.Count);
        Assert.Equal(2, await _database.Db.LeaveEntries.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, await _database.Db.Events.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateLeaveSeriesAsync_WhenSeriesIsNotDraft_Throws()
    {
        var created = await _service.CreateLeaveSeriesAsync(
            CreateSeriesRequest(),
            TestContext.Current.CancellationToken
        );
        await SetSeriesStatusAsync(created.Id, CalendarEventStatusTypeCodes.Active);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateLeaveSeriesAsync(created.Id, CreateSeriesRequest(), TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task UpdateLeaveSeriesAsync_WhenRecurrenceChangedAndEntryNotDraft_Throws()
    {
        var created = await _service.CreateLeaveSeriesAsync(
            CreateSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=2"),
            TestContext.Current.CancellationToken
        );
        await SetEntryStatusAsync(created.LeaveEntryIds.First(), CalendarEventStatusTypeCodes.Active);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateLeaveSeriesAsync(
                created.Id,
                CreateSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=3"),
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task UpdateLeaveSeriesAsync_WhenRecurrenceChangedAndEntryNotDraft_DoesNotPersistSeriesChanges()
    {
        var created = await _service.CreateLeaveSeriesAsync(
            CreateSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=2"),
            TestContext.Current.CancellationToken
        );
        await SetEntryStatusAsync(created.LeaveEntryIds.First(), CalendarEventStatusTypeCodes.Active);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateLeaveSeriesAsync(
                created.Id,
                CreateSeriesRequest(title: "Changed", recurrenceRule: "FREQ=DAILY;COUNT=3"),
                TestContext.Current.CancellationToken
            )
        );

        _database.Db.ChangeTracker.Clear();
        var persisted = await _database
            .Db.EventSeries.AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Series", persisted.Title);
        Assert.Equal("FREQ=DAILY;COUNT=2", persisted.RecurrenceRule);
    }

    [Fact]
    public async Task UpdateLeaveSeriesAsync_WhenRecurrenceUnchanged_PreservesOccurrenceOverrides()
    {
        var created = await _service.CreateLeaveSeriesAsync(
            CreateSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=2"),
            TestContext.Current.CancellationToken
        );
        var cancelledId = created.LeaveEntryIds.Order().First();
        var overridden = await _database
            .Db.LeaveEntries.Include(entry => entry.Event)
            .SingleAsync(entry => entry.Id == cancelledId, TestContext.Current.CancellationToken);
        overridden.Event!.Title = "Occurrence override";
        await _database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _service.UpdateLeaveSeriesAsync(
            created.Id,
            CreateSeriesRequest(title: "Renamed", recurrenceRule: "FREQ=DAILY;COUNT=2", leaveTypeId: _sick.Id),
            TestContext.Current.CancellationToken
        );

        var entries = await _database
            .Db.LeaveEntries.Include(x => x.Event)
            .ToListAsync(TestContext.Current.CancellationToken);
        var cancelled = entries.Single(x => x.Id == cancelledId);
        Assert.Equal("Occurrence override", cancelled.Event!.Title);
        Assert.Equal(_sick.Id, cancelled.LeaveTypeId);
        var active = entries.Single(x => x.Id != cancelledId);
        Assert.Equal("Renamed", active.Event!.Title);
        Assert.Equal(_sick.Id, active.LeaveTypeId);
    }

    [Fact]
    public async Task UpdateLeaveSeriesAsync_WhenUserRemoved_DeletesEntryUserRows()
    {
        var created = await _service.CreateLeaveSeriesAsync(
            CreateSeriesRequest(
                recurrenceRule: "FREQ=DAILY;COUNT=2",
                userIds: [LeaveTestDatabase.UserA, LeaveTestDatabase.UserB]
            ),
            TestContext.Current.CancellationToken
        );

        await _service.UpdateLeaveSeriesAsync(
            created.Id,
            CreateSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=2", userIds: [LeaveTestDatabase.UserA]),
            TestContext.Current.CancellationToken
        );

        var entryUsers = await _database
            .Db.Set<LeaveEntryUser>()
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, entryUsers.Count);
        Assert.All(entryUsers, user => Assert.Equal(LeaveTestDatabase.UserA, user.UserId));
    }

    [Fact]
    public async Task UpdateLeaveSeriesAsync_WhenUserRemoved_DeletesSeriesUserRows()
    {
        var created = await _service.CreateLeaveSeriesAsync(
            CreateSeriesRequest(
                recurrenceRule: "FREQ=DAILY;COUNT=2",
                userIds: [LeaveTestDatabase.UserA, LeaveTestDatabase.UserB]
            ),
            TestContext.Current.CancellationToken
        );

        await _service.UpdateLeaveSeriesAsync(
            created.Id,
            CreateSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=3", userIds: [LeaveTestDatabase.UserA]),
            TestContext.Current.CancellationToken
        );

        var seriesUsers = await _database
            .Db.Set<LeaveSeriesUser>()
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
        var seriesUser = Assert.Single(seriesUsers);
        Assert.Equal(LeaveTestDatabase.UserA, seriesUser.UserId);
        var entryUsers = await _database
            .Db.Set<LeaveEntryUser>()
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, entryUsers.Count);
        Assert.All(entryUsers, user => Assert.Equal(LeaveTestDatabase.UserA, user.UserId));
    }

    [Fact]
    public async Task UpdateLeaveSeriesAsync_WhenPropagationWouldLeaveEntryWithoutUsers_Throws()
    {
        var created = await _service.CreateLeaveSeriesAsync(
            CreateSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=2"),
            TestContext.Current.CancellationToken
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateLeaveSeriesAsync(
                created.Id,
                CreateSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=2", userIds: []),
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task DeleteLeaveSeriesAsync_WhenNotFound_ReturnsFalse()
    {
        Assert.False(await _service.DeleteLeaveSeriesAsync(12345, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteLeaveSeriesAsync_WhenDraft_RemovesSeriesEntriesUsersAndEvents()
    {
        var created = await _service.CreateLeaveSeriesAsync(
            CreateSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=2"),
            TestContext.Current.CancellationToken
        );

        var result = await _service.DeleteLeaveSeriesAsync(created.Id, TestContext.Current.CancellationToken);

        Assert.True(result);
        var db = _database.Db;
        Assert.Empty(await db.LeaveSeries.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await db.LeaveSeriesUsers.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await db.LeaveEntries.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await db.Set<LeaveEntryUser>().ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await db.Events.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await db.EventSeries.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteLeaveSeriesAsync_WhenSeriesIsNotDraft_ThrowsAndKeepsData()
    {
        var created = await _service.CreateLeaveSeriesAsync(
            CreateSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=2"),
            TestContext.Current.CancellationToken
        );
        await SetSeriesStatusAsync(created.Id, CalendarEventStatusTypeCodes.Active);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.DeleteLeaveSeriesAsync(created.Id, TestContext.Current.CancellationToken)
        );
        Assert.Equal(2, await _database.Db.LeaveEntries.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteLeaveSeriesAsync_WhenOccurrenceHasOverrides_RemovesItWithSeries()
    {
        var created = await _service.CreateLeaveSeriesAsync(
            CreateSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=2"),
            TestContext.Current.CancellationToken
        );
        var occurrence = await _database
            .Db.LeaveEntries.Include(entry => entry.Event)
            .FirstAsync(TestContext.Current.CancellationToken);
        occurrence.Event!.Title = "Occurrence override";
        await _database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.True(await _service.DeleteLeaveSeriesAsync(created.Id, TestContext.Current.CancellationToken));
        Assert.Empty(await _database.Db.LeaveEntries.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await _database.Db.Events.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await _database.Db.LeaveSeries.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await _database.Db.EventSeries.ToListAsync(TestContext.Current.CancellationToken));
    }

    private async Task SetEntryStatusAsync(int entryId, string status)
    {
        var entry = await _database
            .Db.LeaveEntries.Include(x => x.Event)
            .SingleAsync(x => x.Id == entryId, TestContext.Current.CancellationToken);
        entry.Event!.StatusTypeCode = status;
        await _database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SetSeriesStatusAsync(int seriesId, string status)
    {
        var series = await _database
            .Db.LeaveSeries.Include(x => x.EventSeries)
            .SingleAsync(x => x.Id == seriesId, TestContext.Current.CancellationToken);
        series.EventSeries!.StatusTypeCode = status;
        await _database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private SchedulingLeaveEntryRequest CreateEntryRequest(
        string title = "Leave",
        int? leaveTypeId = null,
        IReadOnlyCollection<Guid>? userIds = null,
        int? leaveSeriesId = null
    ) =>
        new()
        {
            Title = title,
            StartAtUtc = Start,
            EndAtUtc = End,
            TimeZoneId = "America/Vancouver",
            LocationId = 5,
            LeaveSeriesId = leaveSeriesId,
            LeaveTypeId = leaveTypeId ?? _vacation.Id,
            UserIds = userIds ?? [LeaveTestDatabase.UserA],
        };

    private SchedulingLeaveSeriesRequest CreateSeriesRequest(
        string title = "Series",
        string recurrenceRule = "FREQ=DAILY;COUNT=1",
        int? leaveTypeId = null,
        IReadOnlyCollection<Guid>? userIds = null
    ) =>
        new()
        {
            Title = title,
            RecurrenceRule = recurrenceRule,
            StartAtUtc = Start,
            EndAtUtc = End,
            TimeZoneId = "America/Vancouver",
            LocationId = 5,
            LeaveTypeId = leaveTypeId ?? _vacation.Id,
            UserIds = userIds ?? [LeaveTestDatabase.UserA],
        };
}
