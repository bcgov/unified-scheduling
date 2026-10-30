using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Calendar.Services;
using Unified.Common.Scheduling;
using Unified.Common.Scheduling.Models;
using Unified.Common.Time;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Leave;
using Unified.Leave.Services;
using Unified.Scheduling.Mappings;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public sealed class SchedulingCalendarService(
    ILogger<SchedulingCalendarService> logger,
    UnifiedDbContext db,
    ICalendarTimeZoneResolver timeZoneResolver,
    ITimeZoneService timeZoneService,
    ShiftLoanOutAvailabilityService loanOutAvailabilityService,
    ILeaveCalendarDataProvider? leaveCalendarDataProvider = null,
    ISchedulingCalendarEventDataProvider<SchedulingCalendarEventResponse> schedulingCalendarDataProvider = null
) : ISchedulingCalendarService
{
    public async Task<SchedulingCalendarDataResponse> GetDataAsync(
        SchedulingCalendarRequest request,
        bool includeShifts,
        bool includeAssignments,
        bool includeLeave,
        bool includeLoanOut,
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
        // Use the time zone of the location, not the user's time zone, as a user should view the time an event is occurring at a given location. For example, if I'm viewing an assignment in Calgary, I want to see the time that assignment occurs in Calgary.
        var timeZone = timeZoneResolver.Resolve(
            request.LocationId.HasValue ? null : request.TimeZoneId,
            locationTimeZoneId
        );
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
                .InSchedulingRange(range);
            if (request.LocationId.HasValue)
                query = query.Where(entry =>
                    entry.Event!.LocationId == null || entry.Event.LocationId == request.LocationId.Value
                );
            if (request.UserIds is { Count: > 0 })
                query = query.Where(entry => entry.Users.Any(user => request.UserIds.Contains(user.UserId)));
            var shifts = await query.ToListAsync(cancellationToken);
            var awayUsers = await loanOutAvailabilityService.GetAwayUserIdsAsync(shifts, cancellationToken);
            events.AddRange(
                shifts
                    .Select(shift =>
                        ShiftResponseMapper.ToCalendarEventResponse(shift, awayUsers.GetValueOrDefault(shift))
                    )
                    .Where(response =>
                        request.UserIds is not { Count: > 0 }
                        || response.ResourceIds.Any(id => request.UserIds.Any(userId => userId.ToString() == id))
                    )
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
                .InSchedulingRange(range);
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

        if (includeLeave)
        {
            if (leaveCalendarDataProvider is not null)
            {
                var leaveEvents = await leaveCalendarDataProvider.GetEventsAsync(
                    new CalendarEventQueryContext
                    {
                        StartAtUtc = range.StartAtUtc,
                        EndAtUtc = range.EndAtUtc,
                        LocationId = request.LocationId,
                        UserIds = request.UserIds ?? [],
                    },
                    cancellationToken
                );
                events.AddRange(leaveEvents.Select(SchedulingCalendarEventMapper.ToResponse));
            }
        }
        if (includeLoanOut)
        {
            if (schedulingCalendarDataProvider is not null)
            {
                var loanOutEvents = await schedulingCalendarDataProvider.GetDataAsync(
                    new SchedulingCalendarEventQueryContext
                    {
                        StartAtUtc = range.StartAtUtc,
                        EndAtUtc = range.EndAtUtc,
                        LocationId = request.LocationId,
                        UserIds = request.UserIds ?? [],
                    },
                    cancellationToken
                );
                events.AddRange(loanOutEvents);
            }
        }

        var response = new SchedulingCalendarDataResponse
        {
            Events = events.OrderBy(item => item.Start).ThenBy(item => item.Id).ToList(),
        };
        logger.LogDebug("Scheduling calendar query returned {SchedulingEventCount} events.", response.Events.Count);
        return response;
    }
}
