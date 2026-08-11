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
        Entry.EventId.HasValue && Overlaps.EventId.HasValue
            ? $"conflict:{Math.Min(Entry.EventId.Value, Overlaps.EventId.Value)}:{Math.Max(Entry.EventId.Value, Overlaps.EventId.Value)}:{ResourceId}"
            : $"conflict:candidate:{ResourceId}:{OverlapStart.UtcTicks}:{OverlapEnd.UtcTicks}";
}
