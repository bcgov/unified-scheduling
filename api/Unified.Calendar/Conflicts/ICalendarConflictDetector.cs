namespace Unified.Calendar.Conflicts;

public interface ICalendarConflictDetector
{
    IReadOnlyCollection<CalendarConflict> Detect(IReadOnlyCollection<CalendarConflictParticipant> participants);
}
