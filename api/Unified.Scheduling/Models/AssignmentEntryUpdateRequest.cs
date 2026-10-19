using Unified.Calendar.Models;
using Unified.Calendar.Models;

namespace Unified.Scheduling.Models;

public sealed record AssignmentEntryUpdateRequest : AssignmentEntryRequest
{
    public IReadOnlyCollection<CalendarConflictAcknowledgement>? ConflictOverrides { get; init; }
}
