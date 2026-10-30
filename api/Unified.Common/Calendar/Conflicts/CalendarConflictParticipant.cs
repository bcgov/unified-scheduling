namespace Unified.Common.Calendar.Conflicts;

public sealed record CalendarConflictParticipant(
    string EventId,
    string SourceModule,
    Guid ResourceId,
    DateTimeOffset Start,
    DateTimeOffset End,
    string Title,
    int? SourceEntityId = null,
    string? TimeZoneId = null,
    bool RequiresMatchingTimeZone = false,
    bool IsInQueryScope = true,
    string? CanonicalEventId = null,
    bool AllDay = false,
    // Retained for constructor compatibility; event-type rules now determine reporting.
    string? ConflictsWithSourceModule = null,
    string EventTypeCode = ""
)
{
    public CalendarConflictEventIdentity Identity => CalendarConflictEventIdentity.Create(SourceModule, EventId);
}
