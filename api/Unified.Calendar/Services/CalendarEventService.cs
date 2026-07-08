using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Calendar.Models;
using Unified.Db;
using Unified.Db.Models.Calendar;

namespace Unified.Calendar.Services;

public sealed class CalendarEventService(ILogger<CalendarEventService> logger, UnifiedDbContext db)
    : ICalendarEventService
{
    public async Task<IReadOnlyCollection<CalendarEventResponse>> GetEventsAsync(
        CalendarEventsRequest request,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation(
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

        var eventEntities = await query.OrderBy(e => e.StartAtUtc).ThenBy(e => e.Id).ToListAsync(cancellationToken);

        var events = eventEntities.Select(MapToResponse).ToList();

        logger.LogDebug(
            "Calendar event query completed for range {StartDate} to {EndDate} with location filter {LocationId}; {EventCount} events matched.",
            request.StartDate,
            request.EndDate,
            request.LocationId,
            events.Count
        );

        return events;
    }

    private static CalendarEventResponse MapToResponse(Event eventEntity) =>
        new()
        {
            Id = eventEntity.Id,
            EventSeriesId = eventEntity.EventSeriesId,
            Title = eventEntity.Title,
            Description = eventEntity.Description,
            Notes = eventEntity.Notes,
            Color = eventEntity.Color,
            StartAtUtc = eventEntity.StartAtUtc,
            EndAtUtc = eventEntity.EndAtUtc,
            SeriesStartAtUtc = eventEntity.SeriesStartAtUtc,
            SeriesEndAtUtc = eventEntity.SeriesEndAtUtc,
            TimeZoneId = eventEntity.TimeZoneId,
            AllDay = eventEntity.AllDay,
            IsException = eventEntity.IsException,
            Type = CalendarEventType.CalendarEvent,
            Status = CalendarCodeMappings.ToEventStatus(eventEntity.StatusTypeCode),
            EventTypeCode = CalendarCodeMappings.ToEventTypeCode(eventEntity.EventTypeCode),
            StatusTypeCode = CalendarCodeMappings.ToStatusTypeCode(eventEntity.StatusTypeCode),
            CancelledAt = eventEntity.CancelledAt,
            CancelledByUserId = eventEntity.CancelledByUserId,
            CancellationReason = eventEntity.CancellationReason,
            SourceModule = eventEntity.SourceModule,
            LocationId = eventEntity.LocationId,
        };
}
