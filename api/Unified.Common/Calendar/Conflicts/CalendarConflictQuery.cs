namespace Unified.Common.Calendar.Conflicts;

public sealed record CalendarConflictQuery(
    DateTimeOffset StartAtUtc,
    DateTimeOffset EndAtUtc,
    IReadOnlyCollection<Guid>? ResourceIds = null,
    bool IncludeDraftParticipants = true,
    IReadOnlySet<string>? ViewerPermissions = null,
    IReadOnlyCollection<string>? TimeZoneIds = null
);
