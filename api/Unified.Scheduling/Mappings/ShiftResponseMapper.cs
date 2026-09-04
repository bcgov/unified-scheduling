using Unified.Calendar.Mappings;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Mappings;

internal static class ShiftResponseMapper
{
    private static readonly CalendarEventMapper calendarEventMapper = new CalendarEventMapper();

    public static ShiftSeriesResponse ToShiftSeriesResponse(
        ShiftSeries shiftSeries,
        EventSeries? eventSeries,
        IReadOnlyCollection<ShiftSeriesEntryIds> entryIds
    )
    {
        var userIds = shiftSeries.Users.Select(user => user.UserId).Distinct().ToList();
        var eventIds = entryIds.Select(entry => entry.EventId).ToList();
        var response = calendarEventMapper.ToSeriesResponse(
            () => new ShiftSeriesResponse { Title = string.Empty, StartAtUtc = default },
            eventSeries!,
            userIds,
            eventIds
        );
        return response with
        {
            Id = shiftSeries.Id,
            LunchAvailableMinutes = shiftSeries.LunchAvailableMinutes,
            WorkedLunchMinutes = shiftSeries.WorkedLunchMinutes,
            ShiftEntryIds = entryIds.Select(entry => entry.ShiftEntryId).ToList(),
        };
    }

    public static ShiftEntryResponse ToShiftEntryResponse(
        ShiftEntry shiftEntry,
        IReadOnlyCollection<ShiftAssignmentEntryResponse>? assignmentLinks = null
    )
    {
        var userIds = shiftEntry.Users.Select(user => user.UserId).Distinct().ToList();
        var response = calendarEventMapper.ToEntryResponse(
            () => new ShiftEntryResponse { Title = string.Empty, StartAtUtc = default },
            shiftEntry.Event!,
            userIds
        );
        return response with
        {
            Id = shiftEntry.Id,
            LunchAvailableMinutes = shiftEntry.LunchAvailableMinutes,
            WorkedLunchMinutes = shiftEntry.WorkedLunchMinutes,
            ShiftSeriesId = shiftEntry.ShiftSeriesId,
            EventId = shiftEntry.EventId,
            AssignmentLinks = assignmentLinks ?? [],
        };
    }

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

public sealed record ShiftSeriesEntryIds(int ShiftSeriesId, int ShiftEntryId, int EventId);
