namespace Unified.TimeOff.Models.Scheduling;

public sealed record SchedulingTimeOffEntryRequest : BaseSchedulingEventRequest
{
    public int LeaveTypeId { get; init; }

    public IReadOnlyCollection<Guid> UserIds { get; init; } = [];
}
