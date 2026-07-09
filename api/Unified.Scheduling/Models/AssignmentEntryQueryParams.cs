namespace Unified.Scheduling.Models;

public sealed record AssignmentEntryQueryParams
{
    public int? AssignmentSeriesId { get; init; }

    public int? EventId { get; init; }

    public int? AssignmentTypeId { get; init; }
}
