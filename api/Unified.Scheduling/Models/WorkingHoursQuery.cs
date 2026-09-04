namespace Unified.Scheduling.Models;

public sealed record WorkingHoursQuery
{
    public required DateOnly StartDate { get; init; }

    public required DateOnly EndDate { get; init; }

    public IReadOnlyCollection<Guid>? UserIds { get; init; }

    public IReadOnlyCollection<int>? ShiftLocationIds { get; init; }
}
