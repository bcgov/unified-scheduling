namespace Unified.Scheduling.Models;

public sealed record AssignmentSeriesQueryParams
{
    public int? EventSeriesId { get; init; }

    public int? AssignmentTypeId { get; init; }
}
