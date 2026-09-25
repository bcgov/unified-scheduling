namespace Unified.Calendar.Conflicts;

public sealed class CalendarConflictException(IReadOnlyCollection<CalendarConflict> conflicts)
    : InvalidOperationException("This operation would cause a conflict with an existing event")
{
    public IReadOnlyCollection<CalendarConflict> Conflicts { get; } = conflicts;
}
