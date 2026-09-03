namespace Unified.Calendar.Conflicts;

public sealed record CalendarConflictQuery(
    DateTimeOffset StartAtUtc,
    DateTimeOffset EndAtUtc,
    IReadOnlyCollection<Guid>? ResourceIds = null,
    IReadOnlyCollection<int>? ExcludedEventIds = null,
    bool IncludeDraftParticipants = true
);
