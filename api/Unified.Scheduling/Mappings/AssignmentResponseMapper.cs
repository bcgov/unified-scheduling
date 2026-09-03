using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Mappings;

internal static class AssignmentResponseMapper
{
    public static AssignmentSeriesResponse ToAssignmentSeriesResponse(
        AssignmentSeries assignmentSeries,
        IReadOnlyCollection<AssignmentSeriesEntryIds> entryIds
    ) =>
        new()
        {
            Id = assignmentSeries.Id,
            EventSeriesId = assignmentSeries.EventSeriesId,
            AssignmentDefinitionId = assignmentSeries.AssignmentDefinitionId,
            Title = assignmentSeries.EventSeries?.Title,
            Description = assignmentSeries.EventSeries?.Description,
            Notes = assignmentSeries.EventSeries?.Notes,
            Color = assignmentSeries.EventSeries?.Color,
            RecurrenceRule = assignmentSeries.EventSeries?.RecurrenceRule,
            TimeZoneId = assignmentSeries.EventSeries?.TimeZoneId,
            StartAtUtc = assignmentSeries.EventSeries?.StartAtUtc,
            EndAtUtc = assignmentSeries.EventSeries?.EndAtUtc,
            AllDay = assignmentSeries.EventSeries?.AllDay ?? false,
            EventTypeCode = assignmentSeries.EventSeries?.EventTypeCode,
            StatusTypeCode = assignmentSeries.EventSeries?.StatusTypeCode,
            CancelledAt = assignmentSeries.EventSeries?.CancelledAt,
            CancelledByUserId = assignmentSeries.EventSeries?.CancelledByUserId,
            CancellationReason = assignmentSeries.EventSeries?.CancellationReason,
            LocationId = assignmentSeries.EventSeries?.LocationId,
            CategoryId = assignmentSeries.CategoryId,
            CategoryName = assignmentSeries.Category?.Name,
            SubCategoryId = assignmentSeries.SubCategoryId,
            SubCategoryName = assignmentSeries.SubCategory?.Name,
            Capacity = assignmentSeries.Capacity,
            EventIds = entryIds.Select(entry => entry.EventId).ToList(),
            AssignmentEntryIds = entryIds.Select(entry => entry.AssignmentEntryId).ToList(),
            ShiftSeriesLinks = assignmentSeries
                .ShiftAssignmentSeriesLinks.OrderBy(link => link.ShiftSeriesId)
                .Select(link => new ShiftAssignmentSeriesLinkSummaryResponse
                {
                    Id = link.Id,
                    ShiftSeriesId = link.ShiftSeriesId,
                    AssignmentSeriesId = link.AssignmentSeriesId,
                    AssignedUserIds = link.Users.Select(user => user.UserId).Distinct().ToList(),
                })
                .ToList(),
        };

    public static AssignmentEntryResponse ToAssignmentEntryResponse(AssignmentEntry assignmentEntry)
    {
        var activeLinks = assignmentEntry.ShiftAssignmentEntries.Where(IsActiveShiftAssignmentLink).ToList();
        var assignedUserIds = activeLinks
            .SelectMany(link => link.Users)
            .Select(user => user.UserId)
            .Distinct()
            .ToList();

        return new AssignmentEntryResponse
        {
            Id = assignmentEntry.Id,
            AssignmentSeriesId = assignmentEntry.AssignmentSeriesId,
            EventId = assignmentEntry.EventId,
            AssignmentDefinitionId = assignmentEntry.AssignmentDefinitionId,
            Title = assignmentEntry.Event?.Title,
            Description = assignmentEntry.Event?.Description,
            Notes = assignmentEntry.Event?.Notes,
            Color = assignmentEntry.Event?.Color,
            StartAtUtc = assignmentEntry.Event?.StartAtUtc,
            EndAtUtc = assignmentEntry.Event?.EndAtUtc,
            SeriesStartAtUtc = assignmentEntry.Event?.SeriesStartAtUtc,
            SeriesEndAtUtc = assignmentEntry.Event?.SeriesEndAtUtc,
            TimeZoneId = assignmentEntry.Event?.TimeZoneId,
            AllDay = assignmentEntry.Event?.AllDay ?? false,
            IsException = assignmentEntry.Event?.IsException ?? false,
            EventTypeCode = assignmentEntry.Event?.EventTypeCode,
            StatusTypeCode = assignmentEntry.Event?.StatusTypeCode,
            CancelledAt = assignmentEntry.Event?.CancelledAt,
            CancelledByUserId = assignmentEntry.Event?.CancelledByUserId,
            CancellationReason = assignmentEntry.Event?.CancellationReason,
            LocationId = assignmentEntry.Event?.LocationId,
            CategoryId = assignmentEntry.CategoryId,
            CategoryName = assignmentEntry.Category?.Name,
            SubCategoryId = assignmentEntry.SubCategoryId,
            SubCategoryName = assignmentEntry.SubCategory?.Name,
            Capacity = assignmentEntry.Capacity,
            AssignedUserCount = assignedUserIds.Count,
            LinkedShiftEntryIds = activeLinks.Select(link => link.ShiftEntryId).Distinct().ToList(),
            AssignedUserIds = assignedUserIds,
            AssignmentLinks = activeLinks
                .OrderBy(link => link.ShiftEntryId)
                .Select(link => new ShiftAssignmentEntryResponse
                {
                    Id = link.Id,
                    ShiftEntryId = link.ShiftEntryId,
                    AssignmentEntryId = link.AssignmentEntryId,
                    ShiftAssignmentSeriesLinkId = link.ShiftAssignmentSeriesLinkId,
                    IsException = link.IsException,
                    Capacity = assignmentEntry.Capacity,
                    AssignedUserCount = link.Users.Select(user => user.UserId).Distinct().Count(),
                    UserIds = link.Users.Select(user => user.UserId).Distinct().ToList(),
                })
                .ToList(),
        };
    }

    public static SchedulingCalendarEventResponse ToCalendarEventResponse(AssignmentEntry assignmentEntry)
    {
        var eventEntity = assignmentEntry.Event!;
        var activeLinks = assignmentEntry.ShiftAssignmentEntries.Where(IsActiveShiftAssignmentLink).ToList();
        var assignedUserIds = activeLinks
            .SelectMany(link => link.Users)
            .Select(user => user.UserId)
            .Distinct()
            .ToList();
        return new SchedulingCalendarEventResponse
        {
            Id = $"scheduling.assignment-entry.{assignmentEntry.Id}",
            AssignmentEntryId = assignmentEntry.Id,
            AssignmentSeriesId = assignmentEntry.AssignmentSeriesId,
            EventId = assignmentEntry.EventId,
            UserIds = assignedUserIds,
            ResourceIds = assignedUserIds.Select(userId => userId.ToString()).ToList(),
            Type = "scheduling.assignment",
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
            EventTypeCode = SchedulingConstants.AssignmentEventTypeCode,
            StatusTypeCode = eventEntity.StatusTypeCode,
            CancelledAt = eventEntity.CancelledAt,
            CancelledByUserId = eventEntity.CancelledByUserId,
            CancellationReason = eventEntity.CancellationReason,
            LocationId = eventEntity.LocationId,
            CategoryId = assignmentEntry.CategoryId,
            CategoryName = assignmentEntry.Category?.Name,
            SubCategoryId = assignmentEntry.SubCategoryId,
            SubCategoryName = assignmentEntry.SubCategory?.Name,
            Capacity = assignmentEntry.Capacity,
            AssignedUserCount = assignedUserIds.Count,
            LinkedShiftEntryIds = activeLinks.Select(link => link.ShiftEntryId).Distinct().ToList(),
            AssignedUserIds = assignedUserIds,
        };
    }

    internal static bool IsActiveShiftAssignmentLink(ShiftAssignmentEntry link) =>
        link.Users.Count > 0 && link.ShiftEntry?.Event?.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled;
}

internal sealed record AssignmentSeriesEntryIds(int AssignmentSeriesId, int AssignmentEntryId, int EventId);
