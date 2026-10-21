using Unified.Calendar.Mappings;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Leave;
using Unified.Leave.Constants;
using Unified.Leave.Mappings;

namespace Unified.Tests.Leave.Mappers;

public sealed class SchedulingLeaveMapperTests
{
    private static readonly Guid UserA = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserB = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CancelledBy = new("44444444-4444-4444-4444-444444444444");
    private static readonly DateTimeOffset Start = new(2025, 3, 3, 9, 0, 0, TimeSpan.Zero);

    private readonly SchedulingLeaveEventMapper _mapper = new(new CalendarEventMapper());

    [Fact]
    public void ToSchedulingLeaveEntryResponse_MapsEventSeriesAndUsers()
    {
        var entry = CreateEntry();

        var result = _mapper.ToSchedulingLeaveEntryResponse(entry);

        Assert.Equal(7, result.Id);
        Assert.Equal(3, result.LeaveSeriesId);
        Assert.Equal(2, result.LeaveTypeId);
        Assert.Equal("Leave", result.Title);
        Assert.Equal(Start, result.StartAtUtc);
        Assert.Equal(Start.AddHours(8), result.EndAtUtc);
        Assert.Equal(CalendarEventStatusTypeCodes.Cancelled, result.StatusTypeCode);
        Assert.Equal(CancelledBy, result.CancelledByUserId);
        Assert.Equal(5, result.LocationId);
        Assert.Equal(new[] { UserA }, result.UserIds);
    }

    [Fact]
    public void ToSchedulingLeaveEntryResponse_WhenEventMissing_Throws()
    {
        var entry = new LeaveEntry
        {
            Id = 1,
            EventId = 2,
            LeaveTypeId = 3,
        };

        Assert.Throws<ArgumentNullException>(() => _mapper.ToSchedulingLeaveEntryResponse(entry));
    }

    [Fact]
    public void ToSchedulingLeaveSeriesResponse_MapsSeriesFieldsAndProvidedIds()
    {
        var series = new LeaveSeries
        {
            Id = 3,
            EventSeriesId = 4,
            LeaveTypeId = 2,
            EventSeries = new EventSeries
            {
                Title = "Series",
                Description = "Desc",
                RecurrenceRule = "FREQ=DAILY;COUNT=2",
                StartAtUtc = Start,
                LocationId = 5,
                StatusTypeCode = CalendarEventStatusTypeCodes.Active,
            },
            Users = [new LeaveSeriesUser { UserId = UserA }, new LeaveSeriesUser { UserId = UserA }],
        };

        var result = _mapper.ToSchedulingLeaveSeriesResponse(series, [10, 11], [20, 21]);

        Assert.Equal(3, result.Id);
        Assert.Equal(2, result.LeaveTypeId);
        Assert.Equal("Series", result.Title);
        Assert.Equal("FREQ=DAILY;COUNT=2", result.RecurrenceRule);
        Assert.Equal(5, result.LocationId);
        Assert.Equal(new[] { UserA }, result.UserIds);
        Assert.Equal(new[] { 10, 11 }, result.EventIds);
        Assert.Equal(new[] { 20, 21 }, result.LeaveEntryIds);
    }

    [Fact]
    public void ToLeaveCalendarEvent_MapsEntryAndEventFields()
    {
        var entry = CreateEntry();

        var result = _mapper.ToLeaveCalendarEvent(entry);

        Assert.Equal("scheduling.leave-entry.7", result.Id);
        Assert.Equal("scheduling.leave", result.Type);
        Assert.Equal(7, result.LeaveEntryId);
        Assert.Equal(11, result.EventId);
        Assert.Equal(3, result.LeaveSeriesId);
        Assert.Equal(2, result.LeaveTypeId);
        Assert.Equal(Start, result.Start);
        Assert.Equal(new[] { UserA }, result.UserIds);
        Assert.Equal(CancelledBy, result.CancelledByUserId);
    }

    [Fact]
    public void ToLeaveCalendarEvent_WhenEventMissing_Throws()
    {
        var entry = new LeaveEntry
        {
            Id = 1,
            EventId = 2,
            LeaveTypeId = 3,
        };

        Assert.Throws<InvalidOperationException>(() => _mapper.ToLeaveCalendarEvent(entry));
    }

    private static LeaveEntry CreateEntry() =>
        new()
        {
            Id = 7,
            EventId = 11,
            LeaveSeriesId = 3,
            LeaveTypeId = 2,
            Event = new Event
            {
                Id = 11,
                Title = "Leave",
                StartAtUtc = Start,
                EndAtUtc = Start.AddHours(8),
                IsException = true,
                EventTypeCode = LeaveConstants.LeaveEventTypeCode,
                StatusTypeCode = CalendarEventStatusTypeCodes.Cancelled,
                CancelledByUserId = CancelledBy,
                LocationId = 5,
            },
            LeaveSeries = new LeaveSeries
            {
                Id = 3,
                EventSeries = new EventSeries
                {
                    Title = "Series",
                    RecurrenceRule = "FREQ=DAILY;COUNT=2",
                    StartAtUtc = Start,
                },
                Users = [new LeaveSeriesUser { UserId = UserB }],
            },
            Users = [new LeaveEntryUser { UserId = UserA }, new LeaveEntryUser { UserId = UserA }],
        };
}
