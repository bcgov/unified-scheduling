namespace Unified.Scheduling.Models;

public sealed record ShiftAssignmentSeriesUpdateRequest
{
    public IReadOnlyCollection<Guid> AssignedUserIds { get; init; } = [];
}
