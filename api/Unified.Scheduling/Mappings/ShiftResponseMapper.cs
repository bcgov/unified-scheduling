using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Mappings;

internal static class ShiftResponseMapper
{
    public static ShiftSeriesResponse ToShiftSeriesResponse(
        ShiftSeries shiftSeries,
        EventSeries? eventSeries,
        IReadOnlyCollection<ShiftSeriesEntryIds> entryIds
    ) =>
        new()
        {
            Id = shiftSeries.Id,
            EventSeriesId = shiftSeries.EventSeriesId,
            Title = eventSeries?.Title,
            Description = eventSeries?.Description,
            Notes = eventSeries?.Notes,
            Color = eventSeries?.Color,
            RecurrenceRule = eventSeries?.RecurrenceRule,
            TimeZoneId = eventSeries?.TimeZoneId,
            StartAtUtc = eventSeries?.StartAtUtc,
            EndAtUtc = eventSeries?.EndAtUtc,
            AllDay = eventSeries?.AllDay ?? false,
            EventTypeCode = eventSeries?.EventTypeCode,
            StatusTypeCode = eventSeries?.StatusTypeCode,
            CancelledAt = eventSeries?.CancelledAt,
            CancelledByUserId = eventSeries?.CancelledByUserId,
            CancellationReason = eventSeries?.CancellationReason,
            LocationId = eventSeries?.LocationId,
            LunchAvailableMinutes = shiftSeries.LunchAvailableMinutes,
            WorkedLunchMinutes = shiftSeries.WorkedLunchMinutes,
            UserIds = shiftSeries.Users.Select(user => user.UserId).Distinct().ToList(),
            EventIds = entryIds.Select(entry => entry.EventId).ToList(),
            ShiftEntryIds = entryIds.Select(entry => entry.ShiftEntryId).ToList(),
        };

    public static ShiftEntryResponse ToShiftEntryResponse(
        ShiftEntry shiftEntry,
        IReadOnlyCollection<ShiftAssignmentEntryResponse>? assignmentLinks = null
    ) =>
        new()
        {
            Id = shiftEntry.Id,
            ShiftSeriesId = shiftEntry.ShiftSeriesId,
            EventId = shiftEntry.EventId,
            Title = shiftEntry.Event?.Title,
            StartAtUtc = shiftEntry.Event?.StartAtUtc,
            EndAtUtc = shiftEntry.Event?.EndAtUtc,
            TimeZoneId = shiftEntry.Event?.TimeZoneId,
            StatusTypeCode = shiftEntry.Event?.StatusTypeCode,
            LocationId = shiftEntry.Event?.LocationId,
            LunchAvailableMinutes = shiftEntry.LunchAvailableMinutes,
            WorkedLunchMinutes = shiftEntry.WorkedLunchMinutes,
            UserIds = shiftEntry.Users.Select(user => user.UserId).Distinct().ToList(),
            AssignmentLinks = assignmentLinks ?? [],
        };

    public static IReadOnlyCollection<ShiftAssignmentEntryResponse> ToAssignmentLinkResponses(
        IEnumerable<ShiftAssignmentEntry> links
    ) =>
        links
            .Where(link =>
                link.Users.Count > 0
                && link.AssignmentEntry?.Event?.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
            )
            .Select(link =>
            {
                var userIds = link.Users.Select(user => user.UserId).Distinct().ToList();
                return new ShiftAssignmentEntryResponse
                {
                    Id = link.Id,
                    ShiftEntryId = link.ShiftEntryId,
                    AssignmentEntryId = link.AssignmentEntryId,
                    ShiftAssignmentSeriesLinkId = link.ShiftAssignmentSeriesLinkId,
                    IsException = link.IsException,
                    Capacity = link.AssignmentEntry?.Capacity ?? 0,
                    AssignedUserCount = userIds.Count,
                    UserIds = userIds,
                };
            })
            .ToList();

    public static SchedulingCalendarEventResponse ToCalendarEventResponse(ShiftEntry shiftEntry)
    {
        var eventEntity = shiftEntry.Event!;
        var userIds = shiftEntry.Users.Select(user => user.UserId).Distinct().ToList();

        return new SchedulingCalendarEventResponse
        {
            Id = $"scheduling.shift-entry.{shiftEntry.Id}",
            ShiftEntryId = shiftEntry.Id,
            ShiftSeriesId = shiftEntry.ShiftSeriesId,
            EventId = shiftEntry.EventId,
            UserIds = userIds,
            Type = "scheduling.shift",
            SourceModule = SchedulingConstants.SourceModule,
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
            EventTypeCode = SchedulingConstants.ShiftEventTypeCode,
            StatusTypeCode = eventEntity.StatusTypeCode,
            CancelledAt = eventEntity.CancelledAt,
            CancelledByUserId = eventEntity.CancelledByUserId,
            CancellationReason = eventEntity.CancellationReason,
            LocationId = eventEntity.LocationId,
            ResourceIds = userIds.Select(userId => userId.ToString()).ToList(),
        };
    }
}

internal sealed record ShiftSeriesEntryIds(int ShiftSeriesId, int ShiftEntryId, int EventId);
