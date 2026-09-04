namespace Unified.Scheduling.Models;

public sealed record WorkingHoursResult
{
    public required Guid UserId { get; init; }

    public required DateOnly Date { get; init; }

    public required int PaidShiftMinutes { get; init; }

    public required int WorkedLunchMinutes { get; init; }

    public required int PaidOutsideShiftMinutes { get; init; }

    public required int CreditedMinutes { get; init; }

    public required int OvertimeMinutes { get; init; }
}