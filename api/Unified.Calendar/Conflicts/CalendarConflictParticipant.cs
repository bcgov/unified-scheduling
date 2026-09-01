namespace Unified.Calendar.Conflicts;

public sealed record CalendarConflictParticipant(
    int? EventId,
    string EventTypeCode,
    string SourceModule,
    Guid ResourceId,
    DateTimeOffset Start,
    DateTimeOffset End,
    string Title,
    int? SourceEntityId = null,
    string? TimeZoneId = null
);
