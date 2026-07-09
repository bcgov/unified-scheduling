namespace Unified.Scheduling.Models;

public sealed record ShiftAssignmentSeriesRequest
{
    public int ShiftSeriesId { get; init; }

    public int AssignmentSeriesId { get; init; }

    public IReadOnlyCollection<Guid> UserIds { get; init; } = [];
}
