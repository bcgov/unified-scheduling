namespace Unified.Scheduling.Models;

public sealed record ProposedShiftAssignmentOptionsResponse
{
    public IReadOnlyCollection<AssignmentEntryResponse> EntryOptions { get; init; } = [];

    public IReadOnlyCollection<AssignmentSeriesResponse> SeriesOptions { get; init; } = [];

    public bool HasSameDayNonOverlappingAssignments { get; init; }
}
