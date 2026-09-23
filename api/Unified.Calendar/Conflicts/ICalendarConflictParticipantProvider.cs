namespace Unified.Calendar.Conflicts;

public interface ICalendarConflictParticipantProvider
{
    Task<IReadOnlyCollection<CalendarConflictParticipant>> GetParticipantsAsync(
        CalendarConflictQuery query,
        CancellationToken cancellationToken = default
    );
}
