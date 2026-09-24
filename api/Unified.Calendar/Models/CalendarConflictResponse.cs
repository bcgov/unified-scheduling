using Unified.Calendar.Conflicts;

namespace Unified.Calendar.Models;

public sealed record CalendarConflictEventResponse(
    int EventId,
    string SourceModule,
    string Title,
    DateTimeOffset Start,
    DateTimeOffset End,
    int? SourceEntityId,
    string? TimeZoneId
);

public sealed record CalendarConflictResponse(
    string Id,
    CalendarConflictEventResponse Entry,
    CalendarConflictEventResponse Overlaps,
    Guid ResourceId,
    DateTimeOffset OverlapStart,
    DateTimeOffset OverlapEnd,
    bool IsOverridden,
    string? OverrideNote,
    Guid? CreatedById,
    DateTimeOffset? CreatedOn,
    Guid? UpdatedById,
    DateTimeOffset? UpdatedOn
)
{
    public static CalendarConflictResponse FromConflict(CalendarConflict conflict) =>
        new(
            conflict.Id,
            MapEvent(conflict.Entry),
            MapEvent(conflict.Overlaps),
            conflict.ResourceId,
            conflict.OverlapStart,
            conflict.OverlapEnd,
            conflict.IsOverridden,
            conflict.OverrideNote,
            conflict.CreatedById,
            conflict.CreatedOn,
            conflict.UpdatedById,
            conflict.UpdatedOn
        );

    private static CalendarConflictEventResponse MapEvent(CalendarConflictParticipant participant) =>
        new(
            participant.EventId,
            participant.SourceModule,
            participant.Title,
            participant.Start,
            participant.End,
            participant.SourceEntityId,
            participant.TimeZoneId
        );
}

public sealed record CalendarConflictRejectionResponse(
    string Message,
    IReadOnlyCollection<CalendarConflictResponse> Conflicts
);
