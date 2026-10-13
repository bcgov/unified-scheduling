namespace Unified.TimeOff.Models.Scheduling;

public sealed record SchedulingTimeOffEntryResponse : BaseSchedulingEventResponse
{
    public int Id { get; init; }

    public int? TimeOffSeriesId { get; init; }

    public int LeaveTypeId { get; init; }

    public IReadOnlyCollection<Guid> AssignedUserIds { get; init; } = [];
}
