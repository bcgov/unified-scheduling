using Unified.Calendar.Models;

namespace Unified.Scheduling.Models;

public sealed record ShiftAssignmentEntryUpdateRequest
{
    public IReadOnlyCollection<Guid> UserIds { get; init; } = [];

    public IReadOnlyCollection<CalendarConflictAcknowledgement>? ConflictOverrides { get; init; }
}
