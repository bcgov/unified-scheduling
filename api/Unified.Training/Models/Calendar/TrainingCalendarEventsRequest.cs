namespace Unified.Training.Models.Calendar;

public sealed record TrainingCalendarEventsRequest
{
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public int? LocationId { get; init; }
    public IReadOnlyCollection<Guid>? UserIds { get; init; }
}
