namespace Unified.Scheduling.Models;

public sealed record ScheduleDistributionRequest
{
    public ScheduleDistributionContext? Context { get; init; }

    public IReadOnlyCollection<Guid>? RecipientUserIds { get; init; }
}

public sealed record ScheduleDistributionContext
{
    public string View { get; init; } = string.Empty;

    public int LocationId { get; init; }

    /// <summary>Inclusive start date.</summary>
    public DateOnly StartDate { get; init; }

    /// <summary>Exclusive end date.</summary>
    public DateOnly EndDate { get; init; }

    public string Period { get; init; } = string.Empty;
}

public sealed record ScheduleDistributionResult(int RecipientCount);
