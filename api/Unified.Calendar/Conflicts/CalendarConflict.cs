namespace Unified.Calendar.Conflicts;

public sealed record CalendarConflict(
    CalendarConflictParticipant Entry,
    CalendarConflictParticipant Overlaps,
    Guid ResourceId,
    DateTimeOffset OverlapStart,
    DateTimeOffset OverlapEnd,
    bool IsOverridden = false,
    string? OverrideNote = null,
    Guid? CreatedById = null,
    DateTimeOffset? CreatedOn = null,
    Guid? UpdatedById = null,
    DateTimeOffset? UpdatedOn = null
)
{
    public string Id
    {
        get
        {
            var key = CalendarConflictKey.Create(this);
            return $"conflict:{key.FirstEventId}:{key.SecondEventId}:{key.ResourceId}";
        }
    }
}
