using Unified.Scheduling.Mappings;
using Unified.TimeOff.Models.Calendar;

namespace Unified.Tests.TimeOff.Mappers;

public sealed class SchedulingCalendarEventMapperTests
{
    [Fact]
    public void ToResponse_MapsTimeOffCalendarEventFields()
    {
        var userId = new Guid("11111111-1111-1111-1111-111111111111");
        var cancelledBy = new Guid("44444444-4444-4444-4444-444444444444");
        var start = new DateTimeOffset(2025, 3, 3, 9, 0, 0, TimeSpan.Zero);
        var source = new TimeOffCalendarEvent
        {
            Id = "scheduling.timeoff-entry.7",
            TimeOffEntryId = 7,
            TimeOffSeriesId = 3,
            LeaveTypeId = 2,
            LeaveTypeName = "Vacation",
            EventId = 11,
            UserIds = [userId],
            Type = "scheduling.timeoff",
            SourceModule = "timeoff",
            Title = "Leave",
            Description = "Desc",
            Notes = "Notes",
            Color = "#fff",
            Start = start,
            End = start.AddHours(8),
            SeriesStartAtUtc = start.AddDays(-1),
            SeriesEndAtUtc = start.AddDays(10),
            TimeZoneId = "America/Vancouver",
            AllDay = true,
            IsException = true,
            EventTypeCode = "timeoff",
            StatusTypeCode = "cancelled",
            CancelledAt = start,
            CancelledByUserId = cancelledBy,
            CancellationReason = "Reason",
            LocationId = 5,
        };

        var result = SchedulingCalendarEventMapper.ToResponse(source);

        Assert.Equal(source.Id, result.Id);
        Assert.Equal(7, result.TimeOffEntryId);
        Assert.Equal(3, result.TimeOffSeriesId);
        Assert.Equal(2, result.LeaveTypeId);
        Assert.Equal("Vacation", result.LeaveTypeName);
        Assert.Equal(11, result.EventId);
        Assert.Equal([userId], result.UserIds);
        Assert.Equal("scheduling.timeoff", result.Type);
        Assert.Equal("timeoff", result.SourceModule);
        Assert.Equal("Leave", result.Title);
        Assert.Equal("Desc", result.Description);
        Assert.Equal("Notes", result.Notes);
        Assert.Equal("#fff", result.Color);
        Assert.Equal(start, result.Start);
        Assert.Equal(start.AddHours(8), result.End);
        Assert.Equal(source.SeriesStartAtUtc, result.SeriesStartAtUtc);
        Assert.Equal(source.SeriesEndAtUtc, result.SeriesEndAtUtc);
        Assert.Equal("America/Vancouver", result.TimeZoneId);
        Assert.True(result.AllDay);
        Assert.True(result.IsException);
        Assert.Equal("timeoff", result.EventTypeCode);
        Assert.Equal("cancelled", result.StatusTypeCode);
        Assert.Equal(start, result.CancelledAt);
        Assert.Equal(cancelledBy, result.CancelledByUserId);
        Assert.Equal("Reason", result.CancellationReason);
        Assert.Equal(5, result.LocationId);
        Assert.Null(result.ShiftEntryId);
        Assert.Null(result.AssignmentEntryId);
    }
}
