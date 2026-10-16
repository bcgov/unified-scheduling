using Microsoft.EntityFrameworkCore;
using Unified.Calendar.Services;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Leave;
using Unified.Db.Models.Lookup;
using Unified.Leave.Constants;
using Unified.Leave.Models;
using Unified.Leave.Services;

namespace Unified.Tests.Leave.Services;

public sealed class LeaveSeriesMaterializationHandlerTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Start = new(2026, 6, 1, 16, 0, 0, TimeSpan.Zero);

    private LeaveTestDatabase _database = null!;
    private LeaveSeriesMaterializationHandler _handler = null!;
    private LeaveType _leaveType = null!;
    private LeaveSeries _series = null!;

    public async ValueTask InitializeAsync()
    {
        _database = await LeaveTestDatabase.CreateAsync();
        _handler = new LeaveSeriesMaterializationHandler(_database.Db);

        _leaveType = new LeaveType
        {
            Code = "Vacation",
            Description = "Vacation",
            EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };
        _series = new LeaveSeries
        {
            LeaveType = _leaveType,
            EventSeries = new EventSeries
            {
                Title = "Series",
                EventTypeCode = LeaveConstants.LeaveEventTypeCode,
                StartAtUtc = Start,
            },
        };
        _database.Db.LeaveSeries.Add(_series);
        await _database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public void Identity_UsesLeaveSourceModuleAndEventType()
    {
        Assert.Equal("leave", _handler.SourceModule);
        Assert.Equal("leave", _handler.EventTypeCode);
    }

    [Fact]
    public async Task OnMaterializedEventCreatedAsync_AddsEntryWithUsersAndLeaveType()
    {
        var eventEntity = CreateEvent();
        _database.Db.Events.Add(eventEntity);
        var context = new LeaveSeriesMaterializationContext
        {
            LeaveSeries = _series,
            UserIds = [LeaveTestDatabase.UserA, LeaveTestDatabase.UserB],
        };

        await _handler.OnMaterializedEventCreatedAsync(
            _series.EventSeries!,
            eventEntity,
            new SeriesEntry { StartAtUtc = Start },
            context,
            TestContext.Current.CancellationToken
        );
        await _database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var entry = await _database
            .Db.LeaveEntries.Include(x => x.Users)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(eventEntity.Id, entry.EventId);
        Assert.Equal(_series.Id, entry.LeaveSeriesId);
        Assert.Equal(_leaveType.Id, entry.LeaveTypeId);
        Assert.Equal(
            [LeaveTestDatabase.UserA, LeaveTestDatabase.UserB],
            entry.Users.Select(x => x.UserId).Order().ToArray()
        );
    }

    [Fact]
    public async Task OnMaterializedEventsDeletingAsync_RemovesOnlyEntriesOfDeletedEventsAndTheirUsers()
    {
        var doomed = await AddEntryAsync(LeaveTestDatabase.UserA);
        var kept = await AddEntryAsync(LeaveTestDatabase.UserB);
        var context = new LeaveSeriesMaterializationContext
        {
            LeaveSeries = _series,
            UserIds = [],
            ExistingEntries = [doomed, kept],
        };

        await _handler.OnMaterializedEventsDeletingAsync(
            _series.EventSeries!,
            [doomed.Event!],
            context,
            TestContext.Current.CancellationToken
        );
        await _database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var remaining = await _database
            .Db.LeaveEntries.Include(x => x.Users)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(kept.Id, remaining.Id);
        var userRow = await _database.Db.Set<LeaveEntryUser>().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(LeaveTestDatabase.UserB, userRow.UserId);
    }

    private async Task<LeaveEntry> AddEntryAsync(Guid userId)
    {
        var entry = new LeaveEntry
        {
            LeaveTypeId = _leaveType.Id,
            LeaveSeriesId = _series.Id,
            Event = CreateEvent(),
            Users = [new LeaveEntryUser { UserId = userId }],
        };
        _database.Db.LeaveEntries.Add(entry);
        await _database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return entry;
    }

    private Event CreateEvent() =>
        new()
        {
            Title = "Leave",
            StartAtUtc = Start,
            EndAtUtc = Start.AddHours(8),
            SourceModule = LeaveConstants.SourceModule,
            EventTypeCode = LeaveConstants.LeaveEventTypeCode,
            StatusTypeCode = CalendarEventStatusTypeCodes.Draft,
            EventSeriesId = _series.EventSeriesId,
        };
}
