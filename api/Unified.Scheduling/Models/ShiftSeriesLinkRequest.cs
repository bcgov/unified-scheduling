namespace Unified.Scheduling.Models;

public sealed record ShiftSeriesLinkRequest
{
    public int ShiftSeriesId { get; init; }

    public IReadOnlyCollection<Guid> AssignedUserIds { get; init; } = [];
}
