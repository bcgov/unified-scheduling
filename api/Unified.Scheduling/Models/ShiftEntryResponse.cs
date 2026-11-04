using Unified.Common.Calendar.Scheduling.Models;

namespace Unified.Scheduling.Models;

public sealed record ShiftEntryResponse : CalendarEventEntryResponse
{
    public int? ShiftSeriesId { get; init; }

    public int EventId { get; init; }

    public bool IsException { get; init; }
    public string? ExceptionReason { get; init; }
    public IReadOnlyCollection<ShiftAssignmentEntryResponse> AssignmentLinks { get; init; } = [];
}
