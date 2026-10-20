using Unified.Calendar.Mappings;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Mappings;

internal static class AssignmentResponseMapper
{
    private static readonly CalendarEventMapper calendarEventMapper = new();

    public static AssignmentSeriesResponse ToAssignmentSeriesResponse(
        AssignmentSeries assignmentSeries,
        IReadOnlyCollection<AssignmentSeriesEntryIds> entryIds
    )
    {
        var response = calendarEventMapper.ToSeriesResponse(
            () => new AssignmentSeriesResponse { Title = string.Empty, StartAtUtc = default },
            assignmentSeries.EventSeries!,
            [],
            entryIds.Select(entry => entry.EventId).ToList()
        );

        return response with
        {
            Id = assignmentSeries.Id,
            EventSeriesId = assignmentSeries.EventSeriesId,
            AssignmentDefinitionId = assignmentSeries.AssignmentDefinitionId,
            CategoryId = assignmentSeries.CategoryId,
            CategoryName = assignmentSeries.Category?.Name,
            SubCategoryId = assignmentSeries.SubCategoryId,
            SubCategoryName = assignmentSeries.SubCategory?.Name,
            Capacity = assignmentSeries.Capacity,
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
    }

    public static AssignmentEntryResponse ToAssignmentEntryResponse(AssignmentEntry assignmentEntry)
    {
        var activeLinks = assignmentEntry.ShiftAssignmentEntries.Where(IsActiveShiftAssignmentLink).ToList();
        var assignedUserIds = activeLinks
            .SelectMany(link => link.Users)
            .Select(user => user.UserId)
            .Distinct()
            .ToList();
        var response = calendarEventMapper.ToEntryResponse(
            () => new AssignmentEntryResponse { Title = string.Empty, StartAtUtc = default },
            assignmentEntry.Event!,
            []
        );

        return response with
        {
            Id = assignmentEntry.Id,
            EventSeriesId = null,
            AssignmentSeriesId = assignmentEntry.AssignmentSeriesId,
            EventId = assignmentEntry.EventId,
            AssignmentDefinitionId = assignmentEntry.AssignmentDefinitionId,
            CategoryId = assignmentEntry.CategoryId,
            CategoryName = assignmentEntry.Category?.Name,
            SubCategoryId = assignmentEntry.SubCategoryId,
            SubCategoryName = assignmentEntry.SubCategory?.Name,
            Capacity = assignmentEntry.Capacity,
            AssignedUserCount = assignedUserIds.Count,
            LinkedShiftEntryIds = activeLinks.Select(link => link.ShiftEntryId).Distinct().ToList(),
            AssignedUserIds = assignedUserIds,
            IsException = assignmentEntry.Event?.IsException ?? false,
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
            AssignmentDefinitionId = assignmentEntry.AssignmentDefinitionId,
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
