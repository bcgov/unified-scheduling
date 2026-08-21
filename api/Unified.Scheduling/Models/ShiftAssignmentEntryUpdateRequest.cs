namespace Unified.Scheduling.Models;

public sealed record ShiftAssignmentEntryUpdateRequest
{
    public IReadOnlyCollection<Guid> UserIds { get; init; } = [];
}
