using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Calendar.Services;
using Unified.Common.Time;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.UserManagement;
using Unified.UserManagement.Models;

namespace Unified.UserManagement.Services;

/// <summary>
/// Dedicated calendar data service for leave events, following the same pattern as
/// Unified.Scheduling's ShiftService.GetSchedulingCalendarDataAsync — queries only leave-sourced
/// events (linked via <see cref="UserLeave"/>) in the requested local date range and maps them to
/// the leave-specific calendar event shape.
/// </summary>
public sealed class LeaveCalendarService(
    ILogger<LeaveCalendarService> logger,
    UnifiedDbContext db,
    ICalendarTimeZoneResolver timeZoneResolver,
    ITimeZoneService timeZoneService
) : ILeaveCalendarService
{
    public async Task<LeaveCalendarDataResponse> GetLeaveCalendarDataAsync(
        LeaveCalendarRequest request,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug(
            "Querying leave calendar events for local date range {StartDate} to {EndDate}, timezone {TimeZoneId}, and users {UserIds}.",
            request.StartDate,
            request.EndDate,
            request.TimeZoneId,
            request.UserIds is null ? null : string.Join(",", request.UserIds)
        );

        var timeZone = timeZoneResolver.Resolve(request.TimeZoneId);
        var utcRange = timeZoneService.ConvertInclusiveLocalDateRangeToUtcRange(
            request.StartDate,
            request.EndDate,
            timeZone
        );

        IQueryable<UserLeave> query = db
            .UserLeaves.AsNoTracking()
            .Include(userLeave => userLeave.Event)
            .Include(userLeave => userLeave.LeaveType)
            .Where(userLeave => userLeave.Event.SourceModule == UserManagementConstants.SourceModule)
            .Where(userLeave => userLeave.Event.EventTypeCode == UserManagementConstants.LeaveEventTypeCode)
            .Where(userLeave => userLeave.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled)
            .Where(userLeave => userLeave.Event.StartAtUtc < utcRange.EndAtUtc)
            .Where(userLeave =>
                userLeave.Event.EndAtUtc == null
                    ? userLeave.Event.StartAtUtc >= utcRange.StartAtUtc
                        && userLeave.Event.StartAtUtc < utcRange.EndAtUtc
                    : userLeave.Event.EndAtUtc > utcRange.StartAtUtc
            );

        if (request.UserIds is { Count: > 0 })
            query = query.Where(userLeave => request.UserIds.Contains(userLeave.UserId));

        var leaves = await query
            .OrderBy(userLeave => userLeave.Event.StartAtUtc)
            .ThenBy(userLeave => userLeave.Id)
            .ToListAsync(cancellationToken);

        var response = new LeaveCalendarDataResponse
        {
            Events = leaves.Select(LeaveResponseMapper.ToCalendarEventResponse).ToList(),
        };

        logger.LogDebug("Leave calendar query returned {LeaveEventCount} events.", response.Events.Count);

        return response;
    }
}

internal static class LeaveResponseMapper
{
    public static LeaveCalendarEventResponse ToCalendarEventResponse(UserLeave userLeave)
    {
        var eventEntity = userLeave.Event;
        var leaveType = userLeave.LeaveType;

        return new LeaveCalendarEventResponse
        {
            Id = $"user-management.leave.{userLeave.Id}",
            LeaveId = userLeave.Id,
            EventId = userLeave.EventId,
            UserId = userLeave.UserId,
            Type = "user-management.leave",
            SourceModule = UserManagementConstants.SourceModule,
            Title = eventEntity.Title,
            Description = eventEntity.Description,
            Notes = eventEntity.Notes,
            Color = eventEntity.Color,
            Start = eventEntity.StartAtUtc,
            End = eventEntity.EndAtUtc,
            TimeZoneId = eventEntity.TimeZoneId,
            AllDay = eventEntity.AllDay,
            EventTypeCode = UserManagementConstants.LeaveEventTypeCode,
            StatusTypeCode = eventEntity.StatusTypeCode,
            CancelledAt = eventEntity.CancelledAt,
            CancelledByUserId = eventEntity.CancelledByUserId,
            CancellationReason = eventEntity.CancellationReason,
            LeaveTypeId = userLeave.LeaveTypeId,
            LeaveTypeCode = leaveType?.Code ?? string.Empty,
            IsPaid = leaveType?.IsPaid ?? false,
        };
    }
}
