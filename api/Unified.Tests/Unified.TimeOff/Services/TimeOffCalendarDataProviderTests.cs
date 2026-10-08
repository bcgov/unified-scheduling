using Unified.Common.Calendar;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Lookup;
using Unified.Db.Models.TimeOff;
using Unified.TimeOff.Constants;
using Unified.TimeOff.Models.Calendar;
using Unified.TimeOff.Services;

namespace Unified.Tests.TimeOff.Services;

public sealed class TimeOffCalendarDataProviderTests : IAsyncLifetime
{
    private static readonly DateTimeOffset RangeStart = new(2025, 3, 3, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RangeEnd = new(2025, 3, 10, 0, 0, 0, TimeSpan.Zero);

    private TimeOffTestDatabase _database = null!;
    private TimeOffCalendarDataProvider _provider = null!;
    private LeaveType _leaveType = null!;

    public async ValueTask InitializeAsync()
    {
        _database = await TimeOffTestDatabase.CreateAsync();
        _provider = new TimeOffCalendarDataProvider(_database.Db);

        _leaveType = new LeaveType
        {
            Code = "Vacation",
            Description = "Vacation",
            EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };
        _database.Db.LeaveTypes.Add(_leaveType);
        await _database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task GetEventsAsync_WhenEntryMatches_MapsEventWithLeaveTypeNameAndId()
    {
        var entry = await AddEntryAsync(RangeStart.AddDays(1), users: [TimeOffTestDatabase.UserA]);

        var result = await GetAsync();

        var item = Assert.Single(result);
        Assert.Equal($"scheduling.timeoff-entry.{entry.Id}", item.Id);
        Assert.Equal(entry.Id, item.TimeOffEntryId);
        Assert.Equal(entry.EventId, item.EventId);
        Assert.Equal(_leaveType.Id, item.LeaveTypeId);
        Assert.Equal("Vacation", item.LeaveTypeName);
        Assert.Equal([TimeOffTestDatabase.UserA], item.UserIds);
    }

    [Fact]
    public async Task GetEventsAsync_ExcludesOtherModulesTypesAndCancelledEvents()
    {
        var included = await AddEntryAsync(RangeStart.AddDays(1));
        await AddEntryAsync(RangeStart.AddDays(1), sourceModule: "scheduling");
        await AddEntryAsync(RangeStart.AddDays(1), eventTypeCode: CalendarEventTypeCodes.General);
        await AddEntryAsync(RangeStart.AddDays(1), status: CalendarEventStatusTypeCodes.Cancelled);

        var result = await GetAsync();

        var item = Assert.Single(result);
        Assert.Equal(included.Id, item.TimeOffEntryId);
    }

    [Fact]
    public async Task GetEventsAsync_OnlyReturnsEventsOverlappingHalfOpenRange()
    {
        var inside = await AddEntryAsync(RangeStart.AddDays(1));
        var overlapsStart = await AddEntryAsync(RangeStart.AddHours(-4), RangeStart.AddHours(4));
        await AddEntryAsync(RangeStart.AddDays(-2), RangeStart); // ends exactly at range start
        await AddEntryAsync(RangeEnd, RangeEnd.AddHours(8)); // starts exactly at range end

        var result = await GetAsync();

        Assert.Equal(
            new[] { inside.Id, overlapsStart.Id }.Order(),
            result.Select(x => x.TimeOffEntryId).Order().ToArray()
        );
    }

    [Fact]
    public async Task GetEventsAsync_WhenLocationFilterSet_ReturnsMatchingOrUnassignedLocations()
    {
        var atFive = await AddEntryAsync(RangeStart.AddDays(1), locationId: 5);
        await AddEntryAsync(RangeStart.AddDays(1), locationId: 9);
        var noLocation = await AddEntryAsync(RangeStart.AddDays(1), locationId: null);

        var result = await GetAsync(locationId: 5);

        Assert.Equal(
            new[] { atFive.Id, noLocation.Id }.Order(),
            result.Select(x => x.TimeOffEntryId).Order().ToArray()
        );
    }

    [Fact]
    public async Task GetEventsAsync_WhenUserFilterSet_ReturnsOnlyEntriesForThoseUsers()
    {
        var forA = await AddEntryAsync(RangeStart.AddDays(1), users: [TimeOffTestDatabase.UserA]);
        await AddEntryAsync(RangeStart.AddDays(1), users: [TimeOffTestDatabase.UserB]);

        var result = await GetAsync(userIds: [TimeOffTestDatabase.UserA]);

        var item = Assert.Single(result);
        Assert.Equal(forA.Id, item.TimeOffEntryId);
    }

    [Fact]
    public async Task GetEventsAsync_WhenSeriesEventHasNoEntry_ReturnsSyntheticEntryFromSeries()
    {
        var series = await AddSeriesAsync(users: [TimeOffTestDatabase.UserA]);
        var seriesEvent = AddEvent(
            RangeStart.AddDays(2),
            RangeStart.AddDays(2).AddHours(8),
            eventSeriesId: series.EventSeriesId
        );
        await _database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await GetAsync();

        var item = Assert.Single(result);
        Assert.Equal(seriesEvent.Id, item.EventId);
        Assert.Equal(series.Id, item.TimeOffSeriesId);
        Assert.Equal(_leaveType.Id, item.LeaveTypeId);
        Assert.Equal("Vacation", item.LeaveTypeName);
        Assert.Equal([TimeOffTestDatabase.UserA], item.UserIds);
    }

    [Fact]
    public async Task GetEventsAsync_WhenSeriesEventAlreadyHasEntry_DoesNotDuplicate()
    {
        var series = await AddSeriesAsync(users: [TimeOffTestDatabase.UserA]);
        var entry = await AddEntryAsync(RangeStart.AddDays(2), series: series);

        var result = await GetAsync();

        var item = Assert.Single(result);
        Assert.Equal(entry.Id, item.TimeOffEntryId);
    }

    private async Task<IReadOnlyCollection<TimeOffCalendarEvent>> GetAsync(
        int? locationId = null,
        Guid[]? userIds = null
    ) =>
        await _provider.GetEventsAsync(
            new CalendarEventQueryContext
            {
                StartAtUtc = RangeStart,
                EndAtUtc = RangeEnd,
                LocationId = locationId,
                UserIds = userIds ?? [],
            },
            TestContext.Current.CancellationToken
        );

    private Event AddEvent(
        DateTimeOffset start,
        DateTimeOffset? end = null,
        string sourceModule = TimeOffConstants.SourceModule,
        string eventTypeCode = TimeOffConstants.TimeOffEventTypeCode,
        string status = CalendarEventStatusTypeCodes.Active,
        int? locationId = null,
        int? eventSeriesId = null
    )
    {
        var eventEntity = new Event
        {
            Title = "Leave",
            StartAtUtc = start,
            EndAtUtc = end ?? start.AddHours(8),
            SourceModule = sourceModule,
            EventTypeCode = eventTypeCode,
            StatusTypeCode = status,
            LocationId = locationId,
            EventSeriesId = eventSeriesId,
        };
        _database.Db.Events.Add(eventEntity);
        return eventEntity;
    }

    private async Task<TimeOffSeries> AddSeriesAsync(Guid[]? users = null)
    {
        var eventSeries = new EventSeries
        {
            Title = "Series",
            EventTypeCode = TimeOffConstants.TimeOffEventTypeCode,
            StartAtUtc = RangeStart,
        };
        _database.Db.EventSeries.Add(eventSeries);
        await _database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var series = new TimeOffSeries
        {
            EventSeriesId = eventSeries.Id,
            LeaveTypeId = _leaveType.Id,
            Users = (users ?? []).Select(id => new TimeOffSeriesUser { UserId = id }).ToList(),
        };
        _database.Db.TimeOffSeries.Add(series);
        await _database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return series;
    }

    private async Task<TimeOffEntry> AddEntryAsync(
        DateTimeOffset start,
        DateTimeOffset? end = null,
        string sourceModule = TimeOffConstants.SourceModule,
        string eventTypeCode = TimeOffConstants.TimeOffEventTypeCode,
        string status = CalendarEventStatusTypeCodes.Active,
        int? locationId = null,
        Guid[]? users = null,
        TimeOffSeries? series = null
    )
    {
        var eventEntity = AddEvent(start, end, sourceModule, eventTypeCode, status, locationId, series?.EventSeriesId);
        await _database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var entry = new TimeOffEntry
        {
            LeaveTypeId = _leaveType.Id,
            EventId = eventEntity.Id,
            TimeOffSeriesId = series?.Id,
            Users = (users ?? []).Select(id => new TimeOffEntryUser { UserId = id }).ToList(),
        };
        _database.Db.TimeOffEntries.Add(entry);
        await _database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return entry;
    }
}
