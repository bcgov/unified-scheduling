namespace Unified.Scheduling.Models;

public sealed record AssignmentEntryQueryParams
{
    public int? AssignmentSeriesId { get; init; }

    public int? EventId { get; init; }

    public int? AssignmentTypeId { get; init; }

    public int? LocationId { get; init; }

    public string? StatusTypeCode { get; init; }

    public DateTimeOffset? StartAtUtc { get; init; }

    public DateTimeOffset? EndAtUtc { get; init; }
}
