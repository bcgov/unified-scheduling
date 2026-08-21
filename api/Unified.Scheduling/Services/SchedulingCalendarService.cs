using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Calendar.Services;
using Unified.Common.Time;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public sealed class SchedulingCalendarService(
    ILogger<SchedulingCalendarService> logger,
    UnifiedDbContext db,
    ICalendarTimeZoneResolver timeZoneResolver,
    ITimeZoneService timeZoneService
) : ISchedulingCalendarService
{
    public async Task<SchedulingCalendarDataResponse> GetDataAsync(
        SchedulingCalendarRequest request,
        bool includeShifts,
        bool includeAssignments,
        CancellationToken cancellationToken = default
    )
    {
        var locationTimeZoneId = request.LocationId.HasValue
            ? await db
                .Locations.AsNoTracking()
                .Where(location => location.Id == request.LocationId.Value)
                .Select(location => location.Timezone)
                .SingleOrDefaultAsync(cancellationToken)
            : null;
        var timeZone = timeZoneResolver.Resolve(request.TimeZoneId, locationTimeZoneId);
        var range = timeZoneService.ConvertInclusiveLocalDateRangeToUtcRange(
            request.StartDate,
            request.EndDate,
            timeZone
        );
        var events = new List<SchedulingCalendarEventResponse>();

        if (includeShifts)
        {
            var query = db
                .ShiftEntries.AsNoTracking()
                .Include(entry => entry.Event)
                .Include(entry => entry.Users)
                .Where(entry => entry.Event != null && entry.Event.SourceModule == SchedulingConstants.SourceModule)
                .Where(entry => entry.Event!.EventTypeCode == SchedulingConstants.ShiftEventTypeCode)
                .Where(entry => entry.Event!.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled)
                .Where(entry =>
                    entry.Event!.StartAtUtc < range.EndAtUtc
                    && (entry.Event.EndAtUtc ?? entry.Event.StartAtUtc) > range.StartAtUtc
                );
            if (request.LocationId.HasValue)
                query = query.Where(entry =>
                    entry.Event!.LocationId == null || entry.Event.LocationId == request.LocationId.Value
                );
            if (request.UserIds is { Count: > 0 })
                query = query.Where(entry => entry.Users.Any(user => request.UserIds.Contains(user.UserId)));
            events.AddRange(
                (await query.ToListAsync(cancellationToken)).Select(ShiftResponseMapper.ToCalendarEventResponse)
            );
        }

        if (includeAssignments)
        {
            var query = db
                .AssignmentEntries.AsNoTracking()
                .Include(entry => entry.Event)
                .Include(entry => entry.Category)
                .Include(entry => entry.SubCategory)
                .Include(entry => entry.ShiftAssignmentEntries)
                    .ThenInclude(link => link.Users)
                .Include(entry => entry.ShiftAssignmentEntries)
                    .ThenInclude(link => link.ShiftEntry)
                        .ThenInclude(shiftEntry => shiftEntry!.Event)
                .Where(entry => entry.Event != null && entry.Event.SourceModule == SchedulingConstants.SourceModule)
                .Where(entry => entry.Event!.EventTypeCode == SchedulingConstants.AssignmentEventTypeCode)
                .Where(entry => entry.Event!.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled)
                .Where(entry =>
                    entry.Event!.StartAtUtc < range.EndAtUtc
                    && (entry.Event.EndAtUtc ?? entry.Event.StartAtUtc) > range.StartAtUtc
                );
            if (request.LocationId.HasValue)
                query = query.Where(entry =>
                    entry.Event!.LocationId == null || entry.Event.LocationId == request.LocationId.Value
                );
            if (request.UserIds is { Count: > 0 })
                query = query.Where(entry =>
                    entry.ShiftAssignmentEntries.Any(link =>
                        link.ShiftEntry != null
                        && link.ShiftEntry.Event != null
                        && link.ShiftEntry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
                        && link.Users.Any(user => request.UserIds.Contains(user.UserId))
                    )
                );
            events.AddRange(
                (await query.ToListAsync(cancellationToken)).Select(AssignmentResponseMapper.ToCalendarEventResponse)
            );
        }

        var response = new SchedulingCalendarDataResponse
        {
            Events = events.OrderBy(item => item.Start).ThenBy(item => item.Id).ToList(),
        };
        logger.LogDebug("Scheduling calendar query returned {SchedulingEventCount} events.", response.Events.Count);
        return response;
    }
}
