using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

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
            UserIds = shiftSeries.Users.Select(user => user.UserId).Distinct().ToList(),
            EventIds = entryIds.Select(entry => entry.EventId).ToList(),
            ShiftEntryIds = entryIds.Select(entry => entry.ShiftEntryId).ToList(),
        };

    public static ShiftEntryResponse ToShiftEntryResponse(ShiftEntry shiftEntry) =>
        new()
        {
            Id = shiftEntry.Id,
            ShiftSeriesId = shiftEntry.ShiftSeriesId,
            EventId = shiftEntry.EventId,
            UserIds = shiftEntry.Users.Select(user => user.UserId).Distinct().ToList(),
        };

    public static SchedulingCalendarShiftEventResponse ToCalendarEventResponse(ShiftEntry shiftEntry)
    {
        var eventEntity = shiftEntry.Event!;
        var userIds = shiftEntry.Users.Select(user => user.UserId).Distinct().ToList();

        return new SchedulingCalendarShiftEventResponse
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
