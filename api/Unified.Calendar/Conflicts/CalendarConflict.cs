namespace Unified.Calendar.Conflicts;

public sealed record CalendarConflict(
    CalendarConflictParticipant Entry,
    CalendarConflictParticipant Overlaps,
    Guid ResourceId,
    DateTimeOffset OverlapStart,
    DateTimeOffset OverlapEnd,
    bool IsOverridden = false,
    int? OverrideId = null,
    string? OverrideNote = null,
    Guid? CreatedById = null,
    DateTimeOffset? CreatedOn = null,
    Guid? UpdatedById = null,
    DateTimeOffset? UpdatedOn = null
)
{
    public string Id =>
        CalendarConflictKey.Create(this) is { } key
            ? $"conflict:{key.FirstEventId}:{key.SecondEventId}:{key.ResourceId}"
            : $"conflict:candidate:{ResourceId}:{OverlapStart.UtcTicks}:{OverlapEnd.UtcTicks}";
}
