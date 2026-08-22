namespace Unified.Scheduling.Models;

public sealed record ShiftAssignmentSeriesLinkSummaryResponse
{
    public int Id { get; init; }

    public int ShiftSeriesId { get; init; }

    public int AssignmentSeriesId { get; init; }

    public IReadOnlyCollection<Guid> AssignedUserIds { get; init; } = [];
}
