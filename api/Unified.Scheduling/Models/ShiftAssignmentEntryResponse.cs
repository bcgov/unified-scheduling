namespace Unified.Scheduling.Models;

public sealed record ShiftAssignmentEntryResponse
{
    public int Id { get; init; }

    public int ShiftEntryId { get; init; }

    public int AssignmentEntryId { get; init; }

    public int Capacity { get; init; }

    public int AssignedUserCount { get; init; }

    public IReadOnlyCollection<Guid> UserIds { get; init; } = [];
}
