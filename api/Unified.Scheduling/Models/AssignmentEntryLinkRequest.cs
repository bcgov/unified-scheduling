namespace Unified.Scheduling.Models;

public sealed record AssignmentEntryLinkRequest
{
    public int AssignmentEntryId { get; init; }

    public IReadOnlyCollection<Guid> AssignedUserIds { get; init; } = [];
}
