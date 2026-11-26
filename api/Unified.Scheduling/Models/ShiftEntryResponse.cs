using Unified.Common.Calendar.Scheduling.Models;

namespace Unified.Scheduling.Models;

public sealed record ShiftEntryResponse : CalendarEventEntryResponse
{
    public uint ConcurrencyToken { get; init; }

    public Guid? CreatedById { get; init; }

    public DateTimeOffset? CreatedOn { get; init; }

    public Guid? UpdatedById { get; init; }

    public DateTimeOffset? UpdatedOn { get; init; }

    public int? LunchAvailableMinutes { get; init; }

    public int? WorkedLunchMinutes { get; init; }

    public int? ShiftSeriesId { get; init; }

    public int EventId { get; init; }

    public IReadOnlyCollection<ShiftAssignmentEntryResponse> AssignmentLinks { get; init; } = [];
}
