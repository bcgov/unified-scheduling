namespace Unified.Scheduling.Models;

public sealed record AssignmentSeriesLinkRequest
{
    public int AssignmentSeriesId { get; init; }

    public IReadOnlyCollection<Guid> AssignedUserIds { get; init; } = [];
}
