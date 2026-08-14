namespace Unified.Scheduling.Models;

public sealed record ShiftSeriesQueryParams
{
    public int? EventSeriesId { get; init; }

    public Guid? UserId { get; init; }
}
