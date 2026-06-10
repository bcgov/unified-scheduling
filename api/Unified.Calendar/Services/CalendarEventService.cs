using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Calendar.Models;
using Unified.Db;

namespace Unified.Calendar.Services;

public sealed class CalendarEventService(ILogger<CalendarEventService> logger, UnifiedDbContext db) : ICalendarEventService
{
    public async Task<IReadOnlyCollection<CalendarEventResponse>> GetEventsAsync(
        CalendarEventsRequest request,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug(
            "Querying calendar events for range {StartDate} to {EndDate} with location filter {LocationId}.",
            request.StartDate,
            request.EndDate,
            request.LocationId
        );

        var query = db
            .Events.AsNoTracking()
            .Where(e => e.SourceModule == CalendarConstants.SourceModule)
            .Where(e => e.StartAtUtc < request.EndDate)
            .Where(e => e.EndAtUtc == null ? e.StartAtUtc >= request.StartDate : e.EndAtUtc > request.StartDate);

        if (request.LocationId.HasValue)
        {
            query = query.Where(e => e.LocationId == null || e.LocationId == request.LocationId.Value);
        }

        var events = await query
            .OrderBy(e => e.StartAtUtc)
            .ThenBy(e => e.Id)
            .ProjectToType<CalendarEventResponse>()
            .ToListAsync(cancellationToken);

        logger.LogDebug(
            "Calendar event query completed for range {StartDate} to {EndDate} with location filter {LocationId}; {EventCount} events matched.",
            request.StartDate,
            request.EndDate,
            request.LocationId,
            events.Count
        );

        return events;
    }
}