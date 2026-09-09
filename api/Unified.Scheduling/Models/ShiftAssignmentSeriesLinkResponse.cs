namespace Unified.Scheduling.Models;

public sealed record ShiftAssignmentSeriesLinkResponse
{
    public int Id { get; init; }

    public int ShiftSeriesId { get; init; }

    public int AssignmentSeriesId { get; init; }

    public IReadOnlyCollection<Guid> AssignedUserIds { get; init; } = [];

    public IReadOnlyCollection<int> ShiftAssignmentEntryIds { get; init; } = [];

    public IReadOnlyCollection<ShiftAssignmentEntryResponse> EntryLinks { get; init; } = [];

    public int ExceptionCount { get; init; }
}
