namespace Unified.Scheduling.Models;

public sealed record ShiftEntryQueryParams
{
    public int? ShiftSeriesId { get; init; }

    public int? EventId { get; init; }

    public Guid? UserId { get; init; }
}
