namespace Unified.Scheduling.Models;

public sealed record ShiftEntryLinkRequest
{
    public int ShiftEntryId { get; init; }

    public IReadOnlyCollection<Guid> AssignedUserIds { get; init; } = [];
}
