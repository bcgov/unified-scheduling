using Unified.Calendar.Models;

namespace Unified.Scheduling.Models;

public sealed record ShiftAssignmentSeriesUpdateRequest
{
    public IReadOnlyCollection<Guid> AssignedUserIds { get; init; } = [];

    public IReadOnlyCollection<CalendarConflictAcknowledgement>? ConflictOverrides { get; init; }
}
