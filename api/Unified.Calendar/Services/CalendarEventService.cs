using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Calendar.Models;
using Unified.Db;
using Unified.Db.Models.Calendar;

namespace Unified.Calendar.Services;

public sealed class CalendarEventService(
    ILogger<CalendarEventService> logger,
    UnifiedDbContext db,
    ICalendarDateTimeService calendarDateTimeService
) : ICalendarEventService
{
    public async Task<CalendarDataResponse> GetCalendarDataAsync(
        CalendarDataRequest request,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation(
            "Querying calendar data for local date range {StartDate} to {EndDate}, timezone {TimeZoneId}, and location filter {LocationId}.",
            request.StartDate,
            request.EndDate,
            request.TimeZoneId,
            request.LocationId
        );

        var locationTimeZoneId = await GetLocationTimeZoneIdAsync(request.LocationId, cancellationToken);
        var timeZone = calendarDateTimeService.ResolveTimeZone(request.TimeZoneId, locationTimeZoneId);
        var utcRange = calendarDateTimeService.ConvertInclusiveLocalDateRangeToUtcRange(
            request.StartDate,
            request.EndDate,
            timeZone
        );

        var query = db
            .Events.AsNoTracking()
            .Where(e => e.SourceModule == CalendarConstants.SourceModule)
            .Where(e => e.StartAtUtc < utcRange.EndAtUtc)
            .Where(e =>
                e.EndAtUtc == null
                    ? e.StartAtUtc >= utcRange.StartAtUtc && e.StartAtUtc < utcRange.EndAtUtc
                    : e.EndAtUtc > utcRange.StartAtUtc
            );

        if (request.LocationId.HasValue)
        {
            query = query.Where(e => e.LocationId == null || e.LocationId == request.LocationId.Value);
        }

        var eventEntities = await query.OrderBy(e => e.StartAtUtc).ThenBy(e => e.Id).ToListAsync(cancellationToken);

        var response = new CalendarDataResponse { Events = eventEntities.Select(MapToResponse).ToList() };

        logger.LogDebug(
            "Calendar data query completed for local date range {StartDate} to {EndDate} with location filter {LocationId}; {EventCount} events matched.",
            request.StartDate,
            request.EndDate,
            request.LocationId,
            response.Events.Count
        );

        return response;
    }

    private async Task<string?> GetLocationTimeZoneIdAsync(int? locationId, CancellationToken cancellationToken)
    {
        if (!locationId.HasValue)
            return null;

        return await db
            .Locations.AsNoTracking()
            .Where(location => location.Id == locationId.Value)
            .Select(location => location.Timezone)
            .SingleOrDefaultAsync(cancellationToken);
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
            EventTypeCode = CalendarCodeMappings.ToEventTypeCode(eventEntity.EventTypeCode),
            StatusTypeCode = CalendarCodeMappings.ToStatusTypeCode(eventEntity.StatusTypeCode),
            CancelledAt = eventEntity.CancelledAt,
            CancelledByUserId = eventEntity.CancelledByUserId,
            CancellationReason = eventEntity.CancellationReason,
            SourceModule = eventEntity.SourceModule,
            LocationId = eventEntity.LocationId,
        };
}
