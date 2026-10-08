using Unified.Scheduling.Models;
using Unified.TimeOff.Models.Calendar;

namespace Unified.Scheduling.Mappings;

internal static class SchedulingCalendarEventMapper
{
    public static SchedulingCalendarEventResponse ToResponse(TimeOffCalendarEvent eventData) =>
        new()
        {
            Id = eventData.Id,
            TimeOffEntryId = eventData.TimeOffEntryId,
            TimeOffSeriesId = eventData.TimeOffSeriesId,
            LeaveTypeId = eventData.LeaveTypeId,
            LeaveTypeName = eventData.LeaveTypeName,
            EventId = eventData.EventId,
            UserIds = eventData.UserIds,
            Type = eventData.Type,
            SourceModule = eventData.SourceModule,
            Title = eventData.Title,
            Description = eventData.Description,
            Notes = eventData.Notes,
            Color = eventData.Color,
            Start = eventData.Start,
            End = eventData.End,
            SeriesStartAtUtc = eventData.SeriesStartAtUtc,
            SeriesEndAtUtc = eventData.SeriesEndAtUtc,
            TimeZoneId = eventData.TimeZoneId,
            AllDay = eventData.AllDay,
            IsException = eventData.IsException,
            EventTypeCode = eventData.EventTypeCode,
            StatusTypeCode = eventData.StatusTypeCode,
            CancelledAt = eventData.CancelledAt,
            CancelledByUserId = eventData.CancelledByUserId,
            CancellationReason = eventData.CancellationReason,
            LocationId = eventData.LocationId,
        };
}
