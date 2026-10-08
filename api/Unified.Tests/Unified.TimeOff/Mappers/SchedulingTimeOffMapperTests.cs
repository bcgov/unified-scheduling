using Unified.Db.Models.Calendar;
using Unified.Db.Models.TimeOff;
using Unified.TimeOff.Constants;
using Unified.TimeOff.Mappings;
using Unified.TimeOff.Models.Scheduling;

namespace Unified.Tests.TimeOff.Mappers;

public sealed class SchedulingTimeOffMapperTests
{
    private static readonly Guid UserA = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserB = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CancelledBy = new("44444444-4444-4444-4444-444444444444");
    private static readonly DateTimeOffset Start = new(2025, 3, 3, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ToSchedulingTimeOffEntryResponse_MapsEventSeriesAndUsers()
    {
        var entry = CreateEntry();

        var result = SchedulingTimeOffResponseMapper.ToSchedulingTimeOffEntryResponse(entry);

        Assert.Equal(7, result.Id);
        Assert.Equal(11, result.EventId);
        Assert.Equal(3, result.TimeOffSeriesId);
        Assert.Equal(2, result.LeaveTypeId);
        Assert.Equal("Leave", result.Title);
        Assert.Equal(Start, result.StartAtUtc);
        Assert.Equal(Start.AddHours(8), result.EndAtUtc);
        Assert.True(result.IsException);
        Assert.Equal(CalendarEventStatusTypeCodes.Cancelled, result.StatusTypeCode);
        Assert.Equal(CancelledBy, result.CancelledByUserId);
        Assert.Equal(5, result.LocationId);
        Assert.Equal("FREQ=DAILY;COUNT=2", result.SeriesRecurrenceRule);
        Assert.Equal("Series", result.SeriesTitle);
        Assert.Equal([UserA], result.AssignedUserIds);
        Assert.Equal([UserB], result.SeriesUserIds);
    }

    [Fact]
    public void ToSchedulingTimeOffEntryResponse_WhenEventAndSeriesMissing_UsesDefaults()
    {
        var entry = new TimeOffEntry
        {
            Id = 1,
            EventId = 2,
            LeaveTypeId = 3,
        };

        var result = SchedulingTimeOffResponseMapper.ToSchedulingTimeOffEntryResponse(entry);

        Assert.Null(result.Title);
        Assert.False(result.AllDay);
        Assert.Null(result.SeriesRecurrenceRule);
        Assert.Empty(result.SeriesUserIds);
        Assert.Empty(result.AssignedUserIds);
    }

    [Fact]
    public void ToSchedulingTimeOffSeriesResponse_MapsSeriesFieldsAndProvidedIds()
    {
        var series = new TimeOffSeries
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
            Users = [new TimeOffSeriesUser { UserId = UserA }, new TimeOffSeriesUser { UserId = UserA }],
        };

        var result = SchedulingTimeOffResponseMapper.ToSchedulingTimeOffSeriesResponse(series, [10, 11], [20, 21]);

        Assert.Equal(3, result.Id);
        Assert.Equal(4, result.EventSeriesId);
        Assert.Equal(2, result.LeaveTypeId);
        Assert.Equal("Series", result.Title);
        Assert.Equal("FREQ=DAILY;COUNT=2", result.RecurrenceRule);
        Assert.Equal(5, result.LocationId);
        Assert.Equal([UserA], result.UserIds);
        Assert.Equal([10, 11], result.EventIds);
        Assert.Equal([20, 21], result.TimeOffEntryIds);
    }

    [Fact]
    public void ToCalendarEventResponse_MapsEntryAndEventFields()
    {
        var result = SchedulingTimeOffResponseMapper.ToCalendarEventResponse(CreateEntry(), "Vacation");

        Assert.Equal("scheduling.timeoff-entry.7", result.Id);
        Assert.Equal("scheduling.timeoff", result.Type);
        Assert.Equal(TimeOffConstants.SourceModule, result.SourceModule);
        Assert.Equal(7, result.TimeOffEntryId);
        Assert.Equal(11, result.EventId);
        Assert.Equal(3, result.TimeOffSeriesId);
        Assert.Equal("Vacation", result.LeaveTypeName);
        Assert.Equal(Start, result.Start);
        Assert.Equal([UserA], result.UserIds);
        Assert.Equal(CancelledBy, result.CancelledByUserId);
    }

    [Fact]
    public void ToEvent_TrimsFieldsAndSetsTimeOffDefaults()
    {
        var request = new SchedulingTimeOffEntryRequest
        {
            Title = "  Leave  ",
            Description = " d ",
            StartAtUtc = Start,
            TimeZoneId = " America/Vancouver ",
            LocationId = 5,
        };

        var result = SchedulingTimeOffEventMapper.ToEvent(request, eventSeriesId: 4);

        Assert.Equal("Leave", result.Title);
        Assert.Equal("d", result.Description);
        Assert.Equal("America/Vancouver", result.TimeZoneId);
        Assert.Equal(4, result.EventSeriesId);
        Assert.Equal(TimeOffConstants.SourceModule, result.SourceModule);
        Assert.Equal(TimeOffConstants.TimeOffEventTypeCode, result.EventTypeCode);
        Assert.Equal(CalendarEventStatusTypeCodes.Draft, result.StatusTypeCode);
        Assert.Equal(5, result.LocationId);
    }

    [Fact]
    public void ToEventSeries_TrimsFieldsAndKeepsRecurrenceRule()
    {
        var request = new SchedulingTimeOffSeriesRequest
        {
            Title = "  Series ",
            RecurrenceRule = "FREQ=DAILY;COUNT=2",
            StartAtUtc = Start,
            LocationId = 5,
        };

        var result = SchedulingTimeOffEventMapper.ToEventSeries(request);

        Assert.Equal("Series", result.Title);
        Assert.Equal("FREQ=DAILY;COUNT=2", result.RecurrenceRule);
        Assert.Equal(TimeOffConstants.TimeOffEventTypeCode, result.EventTypeCode);
        Assert.Equal(CalendarEventStatusTypeCodes.Draft, result.StatusTypeCode);
        Assert.Equal(5, result.LocationId);
    }

    private static TimeOffEntry CreateEntry() =>
        new()
        {
            Id = 7,
            EventId = 11,
            TimeOffSeriesId = 3,
            LeaveTypeId = 2,
            Event = new Event
            {
                Id = 11,
                Title = "Leave",
                StartAtUtc = Start,
                EndAtUtc = Start.AddHours(8),
                IsException = true,
                EventTypeCode = TimeOffConstants.TimeOffEventTypeCode,
                StatusTypeCode = CalendarEventStatusTypeCodes.Cancelled,
                CancelledByUserId = CancelledBy,
                LocationId = 5,
            },
            TimeOffSeries = new TimeOffSeries
            {
                Id = 3,
                EventSeries = new EventSeries
                {
                    Title = "Series",
                    RecurrenceRule = "FREQ=DAILY;COUNT=2",
                    StartAtUtc = Start,
                },
                Users = [new TimeOffSeriesUser { UserId = UserB }],
            },
            Users = [new TimeOffEntryUser { UserId = UserA }, new TimeOffEntryUser { UserId = UserA }],
        };
}
