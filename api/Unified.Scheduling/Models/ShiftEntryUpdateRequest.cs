using Unified.Calendar.Models;

namespace Unified.Scheduling.Models;

public sealed record ShiftEntryUpdateRequest : ShiftEntryRequest
{
    public IReadOnlyCollection<CalendarConflictAcknowledgement>? ConflictOverrides { get; init; }
}
