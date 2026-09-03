namespace Unified.Scheduling.Models;

public sealed record ShiftSeriesQueryParams
{
    public int? EventSeriesId { get; init; }

    public Guid? UserId { get; init; }
    public int? LocationId { get; init; }

    public DateTimeOffset? StartAtUtc { get; init; }

    public DateTimeOffset? EndAtUtc { get; init; }
}
