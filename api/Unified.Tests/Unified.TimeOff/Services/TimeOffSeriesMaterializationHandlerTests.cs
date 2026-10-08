using Microsoft.EntityFrameworkCore;
using Unified.Calendar.Services;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Lookup;
using Unified.Db.Models.TimeOff;
using Unified.TimeOff.Constants;
using Unified.TimeOff.Models;
using Unified.TimeOff.Services;

namespace Unified.Tests.TimeOff.Services;

public sealed class TimeOffSeriesMaterializationHandlerTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Start = new(2026, 6, 1, 16, 0, 0, TimeSpan.Zero);

    private TimeOffTestDatabase _database = null!;
    private TimeOffSeriesMaterializationHandler _handler = null!;
    private LeaveType _leaveType = null!;
    private TimeOffSeries _series = null!;

    public async ValueTask InitializeAsync()
    {
        _database = await TimeOffTestDatabase.CreateAsync();
        _handler = new TimeOffSeriesMaterializationHandler(_database.Db);

        _leaveType = new LeaveType
        {
            Code = "Vacation",
            Description = "Vacation",
            EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };
        _series = new TimeOffSeries
        {
            LeaveType = _leaveType,
            EventSeries = new EventSeries
            {
                Title = "Series",
                EventTypeCode = TimeOffConstants.TimeOffEventTypeCode,
                StartAtUtc = Start,
            },
        };
        _database.Db.TimeOffSeries.Add(_series);
        await _database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public void Identity_UsesTimeOffSourceModuleAndEventType()
    {
        Assert.Equal("timeoff", _handler.SourceModule);
        Assert.Equal("timeoff", _handler.EventTypeCode);
    }

    [Fact]
    public async Task OnMaterializedEventCreatedAsync_AddsEntryWithUsersAndLeaveType()
    {
        var eventEntity = CreateEvent();
        _database.Db.Events.Add(eventEntity);
        var context = new TimeOffSeriesMaterializationContext
        {
            TimeOffSeries = _series,
            UserIds = [TimeOffTestDatabase.UserA, TimeOffTestDatabase.UserB],
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
            .Db.TimeOffEntries.Include(x => x.Users)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(eventEntity.Id, entry.EventId);
        Assert.Equal(_series.Id, entry.TimeOffSeriesId);
        Assert.Equal(_leaveType.Id, entry.LeaveTypeId);
        Assert.Equal(
            [TimeOffTestDatabase.UserA, TimeOffTestDatabase.UserB],
            entry.Users.Select(x => x.UserId).Order().ToArray()
        );
    }

    [Fact]
    public async Task OnMaterializedEventsDeletingAsync_RemovesOnlyEntriesOfDeletedEventsAndTheirUsers()
    {
        var doomed = await AddEntryAsync(TimeOffTestDatabase.UserA);
        var kept = await AddEntryAsync(TimeOffTestDatabase.UserB);
        var context = new TimeOffSeriesMaterializationContext
        {
            TimeOffSeries = _series,
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
            .Db.TimeOffEntries.Include(x => x.Users)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(kept.Id, remaining.Id);
        var userRow = await _database.Db.Set<TimeOffEntryUser>().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(TimeOffTestDatabase.UserB, userRow.UserId);
    }

    private async Task<TimeOffEntry> AddEntryAsync(Guid userId)
    {
        var entry = new TimeOffEntry
        {
            LeaveTypeId = _leaveType.Id,
            TimeOffSeriesId = _series.Id,
            Event = CreateEvent(),
            Users = [new TimeOffEntryUser { UserId = userId }],
        };
        _database.Db.TimeOffEntries.Add(entry);
        await _database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return entry;
    }

    private Event CreateEvent() =>
        new()
        {
            Title = "Leave",
            StartAtUtc = Start,
            EndAtUtc = Start.AddHours(8),
            SourceModule = TimeOffConstants.SourceModule,
            EventTypeCode = TimeOffConstants.TimeOffEventTypeCode,
            StatusTypeCode = CalendarEventStatusTypeCodes.Draft,
            EventSeriesId = _series.EventSeriesId,
        };
}
