using Unified.Common.Calendar.Conflicts;

namespace Unified.Calendar.Conflicts;

public readonly record struct CalendarConflictKey(
    CalendarConflictEventIdentity FirstEvent,
    CalendarConflictEventIdentity SecondEvent,
    Guid ResourceId
)
{
    public static CalendarConflictKey Create(
        CalendarConflictEventIdentity firstEvent,
        CalendarConflictEventIdentity secondEvent,
        Guid resourceId
    ) =>
        Compare(firstEvent, secondEvent) <= 0
            ? new CalendarConflictKey(firstEvent, secondEvent, resourceId)
            : new CalendarConflictKey(secondEvent, firstEvent, resourceId);

    public static CalendarConflictKey Create(CalendarConflict conflict) =>
        Create(conflict.Entry.Identity, conflict.Overlaps.Identity, conflict.ResourceId);

    private static int Compare(CalendarConflictEventIdentity left, CalendarConflictEventIdentity right)
    {
        var sourceComparison = StringComparer.Ordinal.Compare(left.SourceModule, right.SourceModule);
        return sourceComparison != 0 ? sourceComparison : StringComparer.Ordinal.Compare(left.EventId, right.EventId);
    }
}
