namespace Unified.Training.Models.Calendar;

public sealed record TrainingCalendarEventsRequest
{
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }

    // Applies to timed events. Omitted values retain UTC range semantics for existing callers.
    public string? TimeZoneId { get; init; }
    public int? LocationId { get; init; }
    public IReadOnlyCollection<Guid>? UserIds { get; init; }
}
