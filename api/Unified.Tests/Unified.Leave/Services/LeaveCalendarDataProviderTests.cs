using Unified.Calendar.Mappings;
using Unified.Common.Calendar;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Leave;
using Unified.Db.Models.Lookup;
using Unified.Leave.Constants;
using Unified.Leave.Mappings;
using Unified.Leave.Models.Calendar;
using Unified.Leave.Services;

namespace Unified.Tests.Leave.Services;

public sealed class LeaveCalendarDataProviderTests : IAsyncLifetime
{
    private static readonly DateTimeOffset RangeStart = new(2025, 3, 3, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RangeEnd = new(2025, 3, 10, 0, 0, 0, TimeSpan.Zero);

    private LeaveTestDatabase _database = null!;
    private LeaveCalendarDataProvider _provider = null!;
    private LeaveType _leaveType = null!;

    public async ValueTask InitializeAsync()
    {
        _database = await LeaveTestDatabase.CreateAsync();
        _provider = new LeaveCalendarDataProvider(
            _database.Db,
            new SchedulingLeaveEventMapper(new CalendarEventMapper())
        );

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
        var entry = await AddEntryAsync(RangeStart.AddDays(1), users: [LeaveTestDatabase.UserA]);

        var result = await GetAsync();

        var item = Assert.Single(result);
        Assert.Equal($"scheduling.leave-entry.{entry.Id}", item.Id);
        Assert.Equal(entry.Id, item.LeaveEntryId);
        Assert.Equal(entry.EventId, item.EventId);
        Assert.Equal(_leaveType.Id, item.LeaveTypeId);
        Assert.Equal("Vacation", item.LeaveTypeName);
        Assert.Equal([LeaveTestDatabase.UserA], item.UserIds);
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
        Assert.Equal(included.Id, item.LeaveEntryId);
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
            result.Select(x => x.LeaveEntryId).Order().ToArray()
        );
    }

    [Fact]
    public async Task GetEventsAsync_WhenLocationFilterSet_ReturnsMatchingOrUnassignedLocations()
    {
        var atFive = await AddEntryAsync(RangeStart.AddDays(1), locationId: 5);
        await AddEntryAsync(RangeStart.AddDays(1), locationId: 9);
        var noLocation = await AddEntryAsync(RangeStart.AddDays(1), locationId: null);

        var result = await GetAsync(locationId: 5);

        Assert.Equal(new[] { atFive.Id, noLocation.Id }.Order(), result.Select(x => x.LeaveEntryId).Order().ToArray());
    }

    [Fact]
    public async Task GetEventsAsync_WhenUserFilterSet_ReturnsOnlyEntriesForThoseUsers()
    {
        var forA = await AddEntryAsync(RangeStart.AddDays(1), users: [LeaveTestDatabase.UserA]);
        await AddEntryAsync(RangeStart.AddDays(1), users: [LeaveTestDatabase.UserB]);

        var result = await GetAsync(userIds: [LeaveTestDatabase.UserA]);

        var item = Assert.Single(result);
        Assert.Equal(forA.Id, item.LeaveEntryId);
    }

    private async Task<IReadOnlyCollection<LeaveCalendarEvent>> GetAsync(
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
        string sourceModule = LeaveConstants.SourceModule,
        string eventTypeCode = LeaveConstants.LeaveEventTypeCode,
        string status = CalendarEventStatusTypeCodes.Active,
        int? locationId = null
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
        };
        _database.Db.Events.Add(eventEntity);
        return eventEntity;
    }

    private async Task<LeaveEntry> AddEntryAsync(
        DateTimeOffset start,
        DateTimeOffset? end = null,
        string sourceModule = LeaveConstants.SourceModule,
        string eventTypeCode = LeaveConstants.LeaveEventTypeCode,
        string status = CalendarEventStatusTypeCodes.Active,
        int? locationId = null,
        Guid[]? users = null
    )
    {
        var eventEntity = AddEvent(start, end, sourceModule, eventTypeCode, status, locationId);
        await _database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var entry = new LeaveEntry
        {
            LeaveTypeId = _leaveType.Id,
            EventId = eventEntity.Id,
            Users = (users ?? []).Select(id => new LeaveEntryUser { UserId = id }).ToList(),
        };
        _database.Db.LeaveEntries.Add(entry);
        await _database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return entry;
    }
}
