namespace Unified.Scheduling.Models;

public sealed record AssignmentSeriesQueryParams
{
    public int? EventSeriesId { get; init; }

    public int? AssignmentTypeId { get; init; }

    public int? LocationId { get; init; }

    public string? StatusTypeCode { get; init; }

    public DateTimeOffset? StartAtUtc { get; init; }

    public DateTimeOffset? EndAtUtc { get; init; }
}
