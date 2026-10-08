namespace Unified.TimeOff.Models.Scheduling;

public sealed record SchedulingTimeOffSeriesResponse : BaseSchedulingSeriesResponse
{
    public int LeaveTypeId { get; init; }

    public IReadOnlyCollection<Guid> UserIds { get; init; } = [];

    public IReadOnlyCollection<int> TimeOffEntryIds { get; init; } = [];
}
