namespace Unified.TimeOff.Models.Scheduling;

public sealed record SchedulingTimeOffSeriesRequest : BaseSchedulingEventRequest
{
    public required string RecurrenceRule { get; init; }

    public int LeaveTypeId { get; init; }

    public IReadOnlyCollection<Guid> UserIds { get; init; } = [];
}
