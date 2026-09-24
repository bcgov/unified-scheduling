namespace Unified.Calendar.Conflicts;

public readonly record struct CalendarConflictKey(int FirstEventId, int SecondEventId, Guid ResourceId)
{
    public static CalendarConflictKey Create(int firstEventId, int secondEventId, Guid resourceId) =>
        new(Math.Min(firstEventId, secondEventId), Math.Max(firstEventId, secondEventId), resourceId);

    public static CalendarConflictKey Create(CalendarConflict conflict) =>
        Create(conflict.Entry.EventId, conflict.Overlaps.EventId, conflict.ResourceId);
}
