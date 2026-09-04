using Unified.Common.Calendar.Scheduling.Models;

namespace Unified.Scheduling.Models;

public sealed record ShiftEntryResponse : CalendarEventEntryResponse
{
    public int LunchAvailableMinutes { get; init; }

    public int WorkedLunchMinutes { get; init; }

    public int? ShiftSeriesId { get; init; }

    public int EventId { get; init; }

    public IReadOnlyCollection<ShiftAssignmentEntryResponse> AssignmentLinks { get; init; } = [];
}
