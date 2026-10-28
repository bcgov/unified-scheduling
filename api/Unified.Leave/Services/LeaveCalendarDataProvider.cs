using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Leave.Constants;
using Unified.Leave.Mappings;
using Unified.Leave.Models.Calendar;

namespace Unified.Leave.Services;

public sealed class LeaveCalendarDataProvider(UnifiedDbContext db, SchedulingLeaveEventMapper mapper)
    : ILeaveCalendarDataProvider
{
    public async Task<IReadOnlyCollection<LeaveCalendarEvent>> GetEventsAsync(
        CalendarEventQueryContext queryContext,
        CancellationToken cancellationToken = default
    )
    {
        var query = db
            .LeaveEntries.AsNoTracking()
            .Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .Include(entry => entry.LeaveType)
            .Where(entry => entry.Event != null && entry.Event.SourceModule == LeaveConstants.SourceModule)
            .Where(entry => entry.Event!.EventTypeCode == LeaveConstants.LeaveEventTypeCode)
            .Where(entry => entry.Event!.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled)
            .Where(entry =>
                entry.Event!.StartAtUtc == null
                || (
                    entry.Event.StartAtUtc < queryContext.EndAtUtc
                    && (entry.Event.EndAtUtc == null
                        ? entry.Event.StartAtUtc >= queryContext.StartAtUtc
                        : entry.Event.EndAtUtc > queryContext.StartAtUtc)
                )
            );
        if (queryContext.LocationId.HasValue)
            query = query.Where(entry =>
                entry.Event!.LocationId == null || entry.Event.LocationId == queryContext.LocationId.Value
            );
        if (queryContext.UserIds is { Count: > 0 })
            query = query.Where(entry => entry.Users.Any(user => queryContext.UserIds.Contains(user.UserId)));

        var entries = await query.ToListAsync(cancellationToken);

        return entries.Select(mapper.ToLeaveCalendarEvent).ToList();
    }
}
