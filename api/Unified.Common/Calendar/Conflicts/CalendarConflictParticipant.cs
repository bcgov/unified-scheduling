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
    string? ConflictsWithSourceModule = null
)
{
    public CalendarConflictEventIdentity Identity => CalendarConflictEventIdentity.Create(SourceModule, EventId);
}
