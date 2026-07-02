namespace Unified.Scheduling.Models;

public sealed record SchedulingCalendarRequest
{
    // Inclusive start date.
    public required DateOnly StartDate { get; init; }

    // Inclusive end date.
    public required DateOnly EndDate { get; init; }

    public string? TimeZoneId { get; init; }

    public int? LocationId { get; init; }

    public IReadOnlyCollection<Guid>? UserIds { get; init; }

    public IReadOnlyDictionary<string, string>? Filters { get; init; }
}
