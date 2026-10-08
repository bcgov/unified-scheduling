using Unified.Db.Models.TimeOff;
using Unified.TimeOff.Constants;
using Unified.TimeOff.Models.Calendar;
using Unified.TimeOff.Models.Scheduling;

namespace Unified.TimeOff.Mappings;

internal static class SchedulingTimeOffResponseMapper
{
    public static SchedulingTimeOffEntryResponse ToSchedulingTimeOffEntryResponse(TimeOffEntry timeOffEntry)
    {
        var eventEntity = timeOffEntry.Event;
        var eventSeries = timeOffEntry.TimeOffSeries?.EventSeries;
        return new SchedulingTimeOffEntryResponse
        {
            Id = timeOffEntry.Id,
            EventId = timeOffEntry.EventId,
            TimeOffSeriesId = timeOffEntry.TimeOffSeriesId,
            LeaveTypeId = timeOffEntry.LeaveTypeId,
            Title = eventEntity?.Title,
            Description = eventEntity?.Description,
            Notes = eventEntity?.Notes,
            Color = eventEntity?.Color,
            StartAtUtc = eventEntity?.StartAtUtc,
            EndAtUtc = eventEntity?.EndAtUtc,
            TimeZoneId = eventEntity?.TimeZoneId,
            AllDay = eventEntity?.AllDay ?? false,
            IsException = eventEntity?.IsException ?? false,
            EventTypeCode = eventEntity?.EventTypeCode,
            StatusTypeCode = eventEntity?.StatusTypeCode,
            CancelledAt = eventEntity?.CancelledAt,
            CancelledByUserId = eventEntity?.CancelledByUserId,
            CancellationReason = eventEntity?.CancellationReason,
            LocationId = eventEntity?.LocationId,
            SeriesRecurrenceRule = eventSeries?.RecurrenceRule,
            SeriesTitle = eventSeries?.Title,
            SeriesStartAtUtc = eventSeries?.StartAtUtc,
            SeriesEndAtUtc = eventSeries?.EndAtUtc,
            SeriesTimeZoneId = eventSeries?.TimeZoneId,
            SeriesUserIds = timeOffEntry.TimeOffSeries?.Users.Select(user => user.UserId).Distinct().ToList() ?? [],
            AssignedUserIds = timeOffEntry.Users.Select(user => user.UserId).Distinct().ToList(),
        };
    }

    public static SchedulingTimeOffSeriesResponse ToSchedulingTimeOffSeriesResponse(
        TimeOffSeries series,
        IReadOnlyCollection<int> eventIds,
        IReadOnlyCollection<int> timeOffEntryIds
    )
    {
        var eventSeries = series.EventSeries;
        return new SchedulingTimeOffSeriesResponse
        {
            Id = series.Id,
            EventSeriesId = series.EventSeriesId,
            LeaveTypeId = series.LeaveTypeId,
            Title = eventSeries?.Title,
            Description = eventSeries?.Description,
            Notes = eventSeries?.Notes,
            Color = eventSeries?.Color,
            RecurrenceRule = eventSeries?.RecurrenceRule,
            TimeZoneId = eventSeries?.TimeZoneId,
            StartAtUtc = eventSeries?.StartAtUtc,
            EndAtUtc = eventSeries?.EndAtUtc,
            AllDay = eventSeries?.AllDay ?? false,
            StatusTypeCode = eventSeries?.StatusTypeCode,
            LocationId = eventSeries?.LocationId,
            UserIds = series.Users.Select(user => user.UserId).Distinct().ToList(),
            EventIds = eventIds,
            TimeOffEntryIds = timeOffEntryIds,
        };
    }

    public static TimeOffCalendarEvent ToCalendarEventResponse(TimeOffEntry timeOffEntry, string? leaveTypeName)
    {
        var userIds = timeOffEntry.Users.Select(user => user.UserId).Distinct().ToList();

        var eventEntity = timeOffEntry.Event!;
        return new TimeOffCalendarEvent
        {
            Id = $"scheduling.timeoff-entry.{timeOffEntry.Id}",
            Type = "scheduling.timeoff",
            SourceModule = TimeOffConstants.SourceModule,
            Title = eventEntity.Title,
            Description = eventEntity.Description,
            Notes = eventEntity.Notes,
            Color = eventEntity.Color,
            Start = eventEntity.StartAtUtc,
            End = eventEntity.EndAtUtc,
            SeriesStartAtUtc = eventEntity.SeriesStartAtUtc,
            SeriesEndAtUtc = eventEntity.SeriesEndAtUtc,
            TimeZoneId = eventEntity.TimeZoneId,
            AllDay = eventEntity.AllDay,
            IsException = eventEntity.IsException,
            EventTypeCode = eventEntity.EventTypeCode,
            StatusTypeCode = eventEntity.StatusTypeCode,
            CancelledAt = eventEntity.CancelledAt,
            CancelledByUserId = eventEntity.CancelledByUserId,
            CancellationReason = eventEntity.CancellationReason,
            LocationId = eventEntity.LocationId,
            UserIds = userIds,
            EventId = timeOffEntry.EventId,
            TimeOffEntryId = timeOffEntry.Id,
            TimeOffSeriesId = timeOffEntry.TimeOffSeriesId,
            LeaveTypeId = timeOffEntry.LeaveTypeId,
            LeaveTypeName = leaveTypeName,
        };
    }
}
