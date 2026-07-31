using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Calendar.Holidays;
using Unified.Calendar.Models;
using Unified.Common.Time;
using Unified.Db;
using Unified.Db.Models.Calendar;

namespace Unified.Calendar.Services;

public sealed class CalendarEventService(
    ILogger<CalendarEventService> logger,
    UnifiedDbContext db,
    StatutoryHolidayCalendarDataProvider statutoryHolidayProvider,
    ICalendarTimeZoneResolver timeZoneResolver,
    ITimeZoneService timeZoneService
) : ICalendarEventService
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

        var locationTimeZoneId = await GetLocationTimeZoneIdAsync(request.LocationId, cancellationToken);
        var timeZone = timeZoneResolver.Resolve(request.TimeZoneId, locationTimeZoneId);
        var utcRange = timeZoneService.ConvertInclusiveLocalDateRangeToUtcRange(
            request.StartDate,
            request.EndDate,
            timeZone
        );

        var persistedEvents = db
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
            persistedEvents = persistedEvents.Where(e =>
                e.LocationId == null || e.LocationId == request.LocationId.Value
            );
        }

        var eventEntities = await persistedEvents
            .OrderBy(e => e.StartAtUtc)
            .ThenBy(e => e.Id)
            .ToListAsync(cancellationToken);

        var holidayEvents = statutoryHolidayProvider.GetEvents(request.StartDate, request.EndDate, timeZone);
        var events = eventEntities
            .Select(MapToResponse)
            .Concat(holidayEvents)
            .OrderBy(calendarEvent => calendarEvent.StartAtUtc)
            .ThenBy(calendarEvent => calendarEvent.Title, StringComparer.Ordinal)
            .ThenBy(calendarEvent => calendarEvent.Id, StringComparer.Ordinal)
            .ToList();

        logger.LogDebug(
            "Calendar event query completed for range {StartDate} to {EndDate} with location filter {LocationId}; {EventCount} events matched.",
            request.StartDate,
            request.EndDate,
            request.LocationId,
            events.Count
        );

        return events;
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
            Id = eventEntity.Id.ToString(CultureInfo.InvariantCulture),
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
