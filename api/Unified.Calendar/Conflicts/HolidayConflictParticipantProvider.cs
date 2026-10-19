using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Unified.Calendar.Holidays;
using Unified.Calendar.Models;
using Unified.Calendar.Services;
using Unified.Common.Calendar.Conflicts;
using Unified.Common.Time;
using Unified.Db;
using Unified.Db.Models.Calendar;

namespace Unified.Calendar.Conflicts;

public sealed class HolidayConflictParticipantProvider(
    UnifiedDbContext db,
    StatutoryHolidayCalendarDataProvider holidayDataProvider,
    ICalendarTimeZoneResolver timeZoneResolver,
    ITimeZoneService timeZoneService
) : ICalendarConflictParticipantProvider
{
    private const string HolidayEventIdPrefix = "stat-holiday:";

    public async Task<CalendarConflictParticipant?> GetParticipantAsync(
        CalendarConflictEventIdentity identity,
        Guid resourceId,
        CancellationToken cancellationToken = default
    )
    {
        if (
            identity.SourceModule != CalendarConstants.SourceModule
            || !TryParseEventId(identity.EventId, out var holidayType, out var holidayDate, out var timeZoneId)
        )
            return null;

        var userIsEnabled = await db.Users.AsNoTracking().AnyAsync(
            user => user.Id == resourceId && user.IsEnabled,
            cancellationToken
        );
        if (!userIsEnabled)
            return null;

        var timeZone = timeZoneResolver.Resolve(timeZoneId);
        var holidayEvent = holidayDataProvider
            .GetEvents(holidayDate, holidayDate, timeZone)
            .SingleOrDefault(calendarEvent => calendarEvent.HolidayType == holidayType);

        return holidayEvent is null ? null : MapParticipant(holidayEvent, resourceId, timeZone.Id);
    }

    public async Task<IReadOnlyCollection<CalendarConflictParticipant>> GetParticipantsAsync(
        CalendarConflictQuery query,
        CancellationToken cancellationToken = default
    )
    {
        if (query.EndAtUtc <= query.StartAtUtc)
            return [];

        var userQuery = db.Users.AsNoTracking().Where(user => user.IsEnabled);
        if (query.ResourceIds is { Count: > 0 })
        {
            var resourceIds = query.ResourceIds.Distinct().ToList();
            userQuery = userQuery.Where(user => resourceIds.Contains(user.Id));
        }

        var userIds = await userQuery.Select(user => user.Id).ToListAsync(cancellationToken);
        if (userIds.Count == 0)
            return [];

        var participants = new List<CalendarConflictParticipant>();
        foreach (var timeZone in ResolveTimeZones(query.TimeZoneIds))
        {
            var startDate = GetLocalDate(query.StartAtUtc, timeZone);
            var endDate = GetLocalDate(query.EndAtUtc.AddTicks(-1), timeZone);
            var holidayEvents = holidayDataProvider
                .GetEvents(startDate, endDate, timeZone)
                .Where(calendarEvent =>
                    calendarEvent.StartAtUtc < query.EndAtUtc
                    && calendarEvent.EndAtUtc.HasValue
                    && calendarEvent.EndAtUtc.Value > query.StartAtUtc
                );

            foreach (var holidayEvent in holidayEvents)
            {
                participants.AddRange(userIds.Select(userId => MapParticipant(holidayEvent, userId, timeZone.Id)));
            }
        }

        return participants;
    }

    private IReadOnlyCollection<TimeZoneInfo> ResolveTimeZones(IReadOnlyCollection<string>? timeZoneIds)
    {
        if (timeZoneIds is not { Count: > 0 })
            return [timeZoneResolver.Resolve(null)];

        var resolvedTimeZones = timeZoneIds
            .Where(timeZoneId => !string.IsNullOrWhiteSpace(timeZoneId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(timeZoneId => timeZoneResolver.Resolve(timeZoneId))
            .DistinctBy(timeZone => timeZone.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return resolvedTimeZones.Count > 0 ? resolvedTimeZones : [timeZoneResolver.Resolve(null)];
    }

    private CalendarConflictParticipant MapParticipant(
        CalendarEventResponse holidayEvent,
        Guid resourceId,
        string timeZoneId
    ) =>
        new(
            BuildEventId(holidayEvent, timeZoneId),
            CalendarConstants.SourceModule,
            resourceId,
            holidayEvent.StartAtUtc,
            holidayEvent.EndAtUtc!.Value,
            holidayEvent.Title,
            TimeZoneId: timeZoneId
        );

    // The time zone is part of the synthetic identity so overrides can reconstruct the
    // location-specific all-day interval without depending on Scheduling data.
    private static string BuildEventId(CalendarEventResponse holidayEvent, string timeZoneId) =>
        $"{holidayEvent.Id}:{timeZoneId}";

    private DateOnly GetLocalDate(DateTimeOffset instant, TimeZoneInfo timeZone) =>
        DateOnly.FromDateTime(timeZoneService.ToLocalUnspecified(instant, timeZone));

    private static bool TryParseEventId(
        string eventId,
        out StatutoryHolidayType holidayType,
        out DateOnly holidayDate,
        out string timeZoneId
    )
    {
        holidayType = default;
        holidayDate = default;
        timeZoneId = string.Empty;

        if (!eventId.StartsWith(HolidayEventIdPrefix, StringComparison.Ordinal))
            return false;

        var parts = eventId.Split(':', 4, StringSplitOptions.None);
        return parts.Length == 4
            && Enum.TryParse(parts[1], ignoreCase: false, out holidayType)
            && DateOnly.TryParseExact(
                parts[2],
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out holidayDate
            )
            && !string.IsNullOrWhiteSpace(timeZoneId = parts[3]);
    }
}
