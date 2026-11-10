namespace Unified.Common.Calendar.Conflicts;

public sealed record CalendarConflictQuery(
    DateTimeOffset StartAtUtc,
    DateTimeOffset EndAtUtc,
    IReadOnlyCollection<Guid>? ResourceIds = null,
    bool IncludeDraftParticipants = true,
    int? LocationId = null,
    IReadOnlyCollection<CalendarConflictResourceTimeZone>? ResourceTimeZones = null
);

public sealed record CalendarConflictResourceTimeZone(Guid ResourceId, string TimeZoneId);
