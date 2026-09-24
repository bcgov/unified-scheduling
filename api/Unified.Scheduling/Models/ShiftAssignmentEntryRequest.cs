using Unified.Calendar.Models;

namespace Unified.Scheduling.Models;

public sealed record ShiftAssignmentEntryRequest
{
    public int ShiftEntryId { get; init; }

    public int AssignmentEntryId { get; init; }

    public IReadOnlyCollection<Guid> UserIds { get; init; } = [];

    public IReadOnlyCollection<CalendarConflictAcknowledgement>? ConflictOverrides { get; init; }
}
