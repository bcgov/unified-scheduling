using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Unified.Calendar.Options;
using Unified.Calendar.Services;
using Unified.Common.Time;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Lookup;
using Unified.Db.Models.TimeOff;
using Unified.TimeOff.Constants;
using Unified.TimeOff.Models.Scheduling;
using Unified.TimeOff.Services;

namespace Unified.Tests.TimeOff.Services;

public sealed class SchedulingTimeOffServiceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Start = new(2026, 6, 1, 16, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = new(2026, 6, 1, 23, 0, 0, TimeSpan.Zero);

    private TimeOffTestDatabase _database = null!;
    private SchedulingTimeOffService _service = null!;
    private LeaveType _vacation = null!;
    private LeaveType _sick = null!;

    public async ValueTask InitializeAsync()
    {
        _database = await TimeOffTestDatabase.CreateAsync();
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

        _service = new SchedulingTimeOffService(
            NullLogger<SchedulingTimeOffService>.Instance,
            db,
            new CalendarLifecycleService(),
            materialization,
            new TimeOffSeriesMaterializationHandler(db)
        );
    }

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    // ---- Entries ----

    [Fact]
    public async Task CreateTimeOffEntryAsync_WhenRequestHasDuplicateUsers_PersistsDraftEntryWithDistinctUsers()
    {
        var result = await _service.CreateTimeOffEntryAsync(
            CreateEntryRequest(
                userIds: [TimeOffTestDatabase.UserA, TimeOffTestDatabase.UserB, TimeOffTestDatabase.UserA]
            ),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(_vacation.Id, result.LeaveTypeId);
        var entry = await _database
            .Db.TimeOffEntries.Include(x => x.Event)
            .Include(x => x.Users)
            .SingleAsync(x => x.Id == result.Id, TestContext.Current.CancellationToken);
        Assert.Equal(CalendarEventStatusTypeCodes.Draft, entry.Event!.StatusTypeCode);
        Assert.Equal(TimeOffConstants.TimeOffEventTypeCode, entry.Event.EventTypeCode);
        Assert.Equal(TimeOffConstants.SourceModule, entry.Event.SourceModule);
        Assert.Equal("Leave", entry.Event.Title);
        Assert.Equal(
            [TimeOffTestDatabase.UserA, TimeOffTestDatabase.UserB],
            entry.Users.Select(x => x.UserId).Order().ToArray()
        );
    }

    [Fact]
    public async Task CreateTimeOffEntryAsync_WhenLeaveTypeDoesNotExist_Throws()
    {
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            _service.CreateTimeOffEntryAsync(
                CreateEntryRequest(leaveTypeId: 9999),
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task GetTimeOffEntryByIdAsync_WhenMissing_ReturnsNull()
    {
        var result = await _service.GetTimeOffEntryByIdAsync(12345, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetTimeOffEntriesByUserAsync_ReturnsOnlyEntriesAssignedToUser()
    {
        var forA = await _service.CreateTimeOffEntryAsync(
            CreateEntryRequest(userIds: [TimeOffTestDatabase.UserA]),
            TestContext.Current.CancellationToken
        );
        await _service.CreateTimeOffEntryAsync(
            CreateEntryRequest(userIds: [TimeOffTestDatabase.UserB]),
            TestContext.Current.CancellationToken
        );

        var result = await _service.GetTimeOffEntriesByUserAsync(
            TimeOffTestDatabase.UserA,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(forA.Id, Assert.Single(result).Id);
    }

    [Fact]
    public async Task UpdateTimeOffEntryAsync_WhenNotFound_ReturnsNull()
    {
        var result = await _service.UpdateTimeOffEntryAsync(
            12345,
            CreateEntryRequest(),
            TestContext.Current.CancellationToken
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateTimeOffEntryAsync_WhenDraft_UpdatesFieldsLeaveTypeAndSyncsUsers()
    {
        var created = await _service.CreateTimeOffEntryAsync(
            CreateEntryRequest(userIds: [TimeOffTestDatabase.UserA]),
            TestContext.Current.CancellationToken
        );

        var result = await _service.UpdateTimeOffEntryAsync(
            created.Id,
            CreateEntryRequest(title: "  Updated  ", leaveTypeId: _sick.Id, userIds: [TimeOffTestDatabase.UserB]),
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(result);
        Assert.Equal("Updated", result.Title);
        Assert.Equal(_sick.Id, result.LeaveTypeId);
        var entry = await _database
            .Db.TimeOffEntries.Include(x => x.Users)
            .SingleAsync(x => x.Id == created.Id, TestContext.Current.CancellationToken);
        Assert.Equal(_sick.Id, entry.LeaveTypeId);
        Assert.Equal([TimeOffTestDatabase.UserB], entry.Users.Select(x => x.UserId).ToArray());
        Assert.Equal(1, await _database.Db.Set<TimeOffEntryUser>().CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateTimeOffEntryAsync_WhenNotDraft_Throws()
    {
        var created = await _service.CreateTimeOffEntryAsync(
            CreateEntryRequest(),
            TestContext.Current.CancellationToken
        );
        await SetEntryStatusAsync(created.Id, CalendarEventStatusTypeCodes.Active);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateTimeOffEntryAsync(
                created.Id,
                CreateEntryRequest(title: "New"),
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task UpdateTimeOffEntryAsync_WhenEventIsOwnedByAnotherModule_Throws()
    {
        var created = await _service.CreateTimeOffEntryAsync(
            CreateEntryRequest(),
            TestContext.Current.CancellationToken
        );
        var entry = await _database
            .Db.TimeOffEntries.Include(x => x.Event)
            .SingleAsync(x => x.Id == created.Id, TestContext.Current.CancellationToken);
        entry.Event!.SourceModule = "scheduling";
        await _database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateTimeOffEntryAsync(created.Id, CreateEntryRequest(), TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task DeleteTimeOffEntryAsync_WhenNotFound_ReturnsFalse()
    {
        var result = await _service.DeleteTimeOffEntryAsync(12345, TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    [Fact]
    public async Task DeleteTimeOffEntryAsync_WhenDraft_RemovesEntryUsersAndEvent()
    {
        var created = await _service.CreateTimeOffEntryAsync(
            CreateEntryRequest(userIds: [TimeOffTestDatabase.UserA, TimeOffTestDatabase.UserB]),
            TestContext.Current.CancellationToken
        );

        var result = await _service.DeleteTimeOffEntryAsync(created.Id, TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.Empty(await _database.Db.TimeOffEntries.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await _database.Db.Set<TimeOffEntryUser>().ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await _database.Db.Events.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteTimeOffEntryAsync_WhenNotDraft_Throws()
    {
        var created = await _service.CreateTimeOffEntryAsync(
            CreateEntryRequest(),
            TestContext.Current.CancellationToken
        );
        await SetEntryStatusAsync(created.Id, CalendarEventStatusTypeCodes.Active);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.DeleteTimeOffEntryAsync(created.Id, TestContext.Current.CancellationToken)
        );
        Assert.Single(await _database.Db.TimeOffEntries.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteTimeOffEntryAsync_WhenEntryBelongsToSeries_Throws()
    {
        var series = await _service.CreateTimeOffSeriesAsync(
            CreateSeriesRequest(),
            TestContext.Current.CancellationToken
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.DeleteTimeOffEntryAsync(series.TimeOffEntryIds.First(), TestContext.Current.CancellationToken)
        );
    }

    // ---- Series ----

    [Fact]
    public async Task CreateTimeOffSeriesAsync_WhenValid_MaterializesDraftEntriesWithUsersAndLeaveType()
    {
        var result = await _service.CreateTimeOffSeriesAsync(
            CreateSeriesRequest(
                recurrenceRule: "FREQ=DAILY;COUNT=3",
                userIds: [TimeOffTestDatabase.UserA, TimeOffTestDatabase.UserA, TimeOffTestDatabase.UserB]
            ),
            TestContext.Current.CancellationToken
        );

        Assert.Equal([TimeOffTestDatabase.UserA, TimeOffTestDatabase.UserB], result.UserIds.Order().ToArray());
        Assert.Equal(3, result.EventIds.Count);
        Assert.Equal(3, result.TimeOffEntryIds.Count);
        Assert.Equal(CalendarEventStatusTypeCodes.Draft, result.StatusTypeCode);

        var entries = await _database
            .Db.TimeOffEntries.Include(x => x.Users)
            .Include(x => x.Event)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, entries.Count);
        Assert.All(
            entries,
            entry =>
            {
                Assert.Equal(result.Id, entry.TimeOffSeriesId);
                Assert.Equal(_vacation.Id, entry.LeaveTypeId);
                Assert.Equal(TimeOffConstants.SourceModule, entry.Event!.SourceModule);
                Assert.Equal(2, entry.Users.Count);
            }
        );
    }

    [Fact]
    public async Task CreateTimeOffSeriesAsync_WhenRuleIsUnbounded_ThrowsAndPersistsNothing()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateTimeOffSeriesAsync(
                CreateSeriesRequest(recurrenceRule: "FREQ=DAILY"),
                TestContext.Current.CancellationToken
            )
        );

        _database.Db.ChangeTracker.Clear();
        Assert.Empty(await _database.Db.TimeOffSeries.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await _database.Db.TimeOffEntries.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetTimeOffSeriesByIdAsync_WhenMissing_ReturnsNull()
    {
        Assert.Null(await _service.GetTimeOffSeriesByIdAsync(12345, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetTimeOffSeriesByUserAsync_ReturnsOnlySeriesForUser()
    {
        var forA = await _service.CreateTimeOffSeriesAsync(
            CreateSeriesRequest(userIds: [TimeOffTestDatabase.UserA]),
            TestContext.Current.CancellationToken
        );
        await _service.CreateTimeOffSeriesAsync(
            CreateSeriesRequest(userIds: [TimeOffTestDatabase.UserB]),
            TestContext.Current.CancellationToken
        );

        var result = await _service.GetTimeOffSeriesByUserAsync(
            TimeOffTestDatabase.UserA,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(forA.Id, Assert.Single(result).Id);
    }

    [Fact]
    public async Task UpdateTimeOffSeriesAsync_WhenNotFound_ReturnsNull()
    {
        var result = await _service.UpdateTimeOffSeriesAsync(
            12345,
            CreateSeriesRequest(),
            TestContext.Current.CancellationToken
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateTimeOffSeriesAsync_WhenRecurrenceUnchanged_PropagatesCopiedFieldsLeaveTypeAndUsers()
    {
        var created = await _service.CreateTimeOffSeriesAsync(
            CreateSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=2", userIds: [TimeOffTestDatabase.UserA]),
            TestContext.Current.CancellationToken
        );
        var entryIds = created.TimeOffEntryIds.ToArray();

        var result = await _service.UpdateTimeOffSeriesAsync(
            created.Id,
            CreateSeriesRequest(
                title: "Renamed",
                recurrenceRule: "FREQ=DAILY;COUNT=2",
                leaveTypeId: _sick.Id,
                userIds: [TimeOffTestDatabase.UserA, TimeOffTestDatabase.UserB]
            ),
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(result);
        Assert.Equal("Renamed", result.Title);
        Assert.Equal(_sick.Id, result.LeaveTypeId);
        Assert.Equal(entryIds, result.TimeOffEntryIds.Order().ToArray());
        var entries = await _database
            .Db.TimeOffEntries.Include(x => x.Event)
            .Include(x => x.Users)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.All(
            entries,
            entry =>
            {
                Assert.Equal("Renamed", entry.Event!.Title);
                Assert.Equal(_sick.Id, entry.LeaveTypeId);
                Assert.Equal(
                    [TimeOffTestDatabase.UserA, TimeOffTestDatabase.UserB],
                    entry.Users.Select(x => x.UserId).Order().ToArray()
                );
            }
        );
    }

    [Fact]
    public async Task UpdateTimeOffSeriesAsync_WhenRecurrenceChanged_RegeneratesDraftEntries()
    {
        var created = await _service.CreateTimeOffSeriesAsync(
            CreateSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=3"),
            TestContext.Current.CancellationToken
        );

        var result = await _service.UpdateTimeOffSeriesAsync(
            created.Id,
            CreateSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=2"),
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(result);
        Assert.Equal(2, result.EventIds.Count);
        Assert.Equal(2, result.TimeOffEntryIds.Count);
        Assert.Equal(2, await _database.Db.TimeOffEntries.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, await _database.Db.Events.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateTimeOffSeriesAsync_WhenSeriesIsNotDraft_Throws()
    {
        var created = await _service.CreateTimeOffSeriesAsync(
            CreateSeriesRequest(),
            TestContext.Current.CancellationToken
        );
        await SetSeriesStatusAsync(created.Id, CalendarEventStatusTypeCodes.Active);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateTimeOffSeriesAsync(created.Id, CreateSeriesRequest(), TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task UpdateTimeOffSeriesAsync_WhenRecurrenceChangedAndEntryNotDraft_Throws()
    {
        var created = await _service.CreateTimeOffSeriesAsync(
            CreateSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=2"),
            TestContext.Current.CancellationToken
        );
        await SetEntryStatusAsync(created.TimeOffEntryIds.First(), CalendarEventStatusTypeCodes.Active);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateTimeOffSeriesAsync(
                created.Id,
                CreateSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=3"),
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task DeleteTimeOffSeriesAsync_WhenNotFound_ReturnsFalse()
    {
        Assert.False(await _service.DeleteTimeOffSeriesAsync(12345, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteTimeOffSeriesAsync_WhenDraft_RemovesSeriesEntriesUsersAndEvents()
    {
        var created = await _service.CreateTimeOffSeriesAsync(
            CreateSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=2"),
            TestContext.Current.CancellationToken
        );

        var result = await _service.DeleteTimeOffSeriesAsync(created.Id, TestContext.Current.CancellationToken);

        Assert.True(result);
        var db = _database.Db;
        Assert.Empty(await db.TimeOffSeries.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await db.TimeOffSeriesUsers.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await db.TimeOffEntries.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await db.Set<TimeOffEntryUser>().ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await db.Events.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await db.EventSeries.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteTimeOffSeriesAsync_WhenAnEntryIsNotDraft_ThrowsAndKeepsData()
    {
        var created = await _service.CreateTimeOffSeriesAsync(
            CreateSeriesRequest(recurrenceRule: "FREQ=DAILY;COUNT=2"),
            TestContext.Current.CancellationToken
        );
        await SetEntryStatusAsync(created.TimeOffEntryIds.First(), CalendarEventStatusTypeCodes.Active);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.DeleteTimeOffSeriesAsync(created.Id, TestContext.Current.CancellationToken)
        );
        Assert.Equal(2, await _database.Db.TimeOffEntries.CountAsync(TestContext.Current.CancellationToken));
    }

    private async Task SetEntryStatusAsync(int entryId, string status)
    {
        var entry = await _database
            .Db.TimeOffEntries.Include(x => x.Event)
            .SingleAsync(x => x.Id == entryId, TestContext.Current.CancellationToken);
        entry.Event!.StatusTypeCode = status;
        await _database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SetSeriesStatusAsync(int seriesId, string status)
    {
        var series = await _database
            .Db.TimeOffSeries.Include(x => x.EventSeries)
            .SingleAsync(x => x.Id == seriesId, TestContext.Current.CancellationToken);
        series.EventSeries!.StatusTypeCode = status;
        await _database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private SchedulingTimeOffEntryRequest CreateEntryRequest(
        string title = "Leave",
        int? leaveTypeId = null,
        IReadOnlyCollection<Guid>? userIds = null
    ) =>
        new()
        {
            Title = title,
            StartAtUtc = Start,
            EndAtUtc = End,
            TimeZoneId = "America/Vancouver",
            LocationId = 5,
            LeaveTypeId = leaveTypeId ?? _vacation.Id,
            UserIds = userIds ?? [TimeOffTestDatabase.UserA],
        };

    private SchedulingTimeOffSeriesRequest CreateSeriesRequest(
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
            UserIds = userIds ?? [TimeOffTestDatabase.UserA],
        };
}
