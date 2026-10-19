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
) : IContextualCalendarConflictParticipantProvider
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

        var contexts = ResolveContexts(query);
        if (contexts.Count == 0)
            return [];

        var holidaysByTimeZone = contexts
            .Select(context => context.TimeZone)
            .DistinctBy(timeZone => timeZone.Id, StringComparer.OrdinalIgnoreCase)
            .Select(timeZone => new
            {
                TimeZone = timeZone,
                Events = GetOverlappingHolidayEvents(query, timeZone),
            })
            .Where(item => item.Events.Count > 0)
            .ToDictionary(item => item.TimeZone.Id, item => item.Events, StringComparer.OrdinalIgnoreCase);
        if (holidaysByTimeZone.Count == 0)
            return [];

        var userQuery = db.Users.AsNoTracking().Where(user => user.IsEnabled);
        var contextualResourceIds = contexts
            .Where(context => context.ResourceId.HasValue)
            .Select(context => context.ResourceId!.Value)
            .Distinct()
            .ToList();
        if (contextualResourceIds.Count > 0)
        {
            userQuery = userQuery.Where(user => contextualResourceIds.Contains(user.Id));
        }
        else if (query.ResourceIds is { Count: > 0 })
        {
            var resourceIds = query.ResourceIds.Distinct().ToList();
            userQuery = userQuery.Where(user => resourceIds.Contains(user.Id));
        }

        var userIds = await userQuery.Select(user => user.Id).ToListAsync(cancellationToken);
        if (userIds.Count == 0)
            return [];

        var participants = new List<CalendarConflictParticipant>();
        var enabledUserIds = userIds.ToHashSet();
        foreach (var context in contexts)
        {
            if (!holidaysByTimeZone.TryGetValue(context.TimeZone.Id, out var holidayEvents))
                continue;

            var applicableUserIds = context.ResourceId.HasValue
                ? enabledUserIds.Contains(context.ResourceId.Value)
                    ? [context.ResourceId.Value]
                    : []
                : userIds;
            foreach (var holidayEvent in holidayEvents)
            {
                participants.AddRange(
                    applicableUserIds.Select(userId => MapParticipant(holidayEvent, userId, context.TimeZone.Id))
                );
            }
        }

        return participants
            .DistinctBy(participant => (participant.EventId, participant.ResourceId))
            .ToList();
    }

    private IReadOnlyCollection<HolidayContext> ResolveContexts(CalendarConflictQuery query)
    {
        if (query.ResourceTimeZones is not null)
        {
            var allowedResourceIds = query.ResourceIds?.ToHashSet();
            if (query.ResourceTimeZones.Any(context => string.IsNullOrWhiteSpace(context.TimeZoneId)))
                throw new InvalidOperationException("A resource holiday context requires an operational time zone.");

            return query
                .ResourceTimeZones.Where(context =>
                    allowedResourceIds is not { Count: > 0 } || allowedResourceIds.Contains(context.ResourceId)
                )
                .Select(context => new HolidayContext(
                    context.ResourceId,
                    timeZoneResolver.Resolve(context.TimeZoneId)
                ))
                .DistinctBy(
                    context => (context.ResourceId, context.TimeZone.Id.ToUpperInvariant())
                )
                .ToList();
        }

        var timeZones = ResolveTimeZones(query.TimeZoneIds);
        if (timeZones.Count > 1)
            throw new InvalidOperationException(
                "Multiple operational time zones require resource-specific holiday contexts."
            );

        return [new HolidayContext(null, timeZones.Single())];
    }

    private IReadOnlyCollection<CalendarEventResponse> GetOverlappingHolidayEvents(
        CalendarConflictQuery query,
        TimeZoneInfo timeZone
    )
    {
        var startDate = GetLocalDate(query.StartAtUtc, timeZone);
        var endDate = GetLocalDate(query.EndAtUtc.AddTicks(-1), timeZone);
        return holidayDataProvider
            .GetEvents(startDate, endDate, timeZone)
            .Where(calendarEvent =>
                calendarEvent.StartAtUtc < query.EndAtUtc
                && calendarEvent.EndAtUtc.HasValue
                && calendarEvent.EndAtUtc.Value > query.StartAtUtc
            )
            .ToList();
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
            TimeZoneId: timeZoneId,
            RequiresMatchingTimeZone: true,
            IsInQueryScope: false,
            CanonicalEventId: holidayEvent.Id
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

    private sealed record HolidayContext(Guid? ResourceId, TimeZoneInfo TimeZone);
}
