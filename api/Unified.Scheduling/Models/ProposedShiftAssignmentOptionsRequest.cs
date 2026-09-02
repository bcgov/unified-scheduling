namespace Unified.Scheduling.Models;

public sealed record ProposedShiftAssignmentOptionsRequest
{
    public required int LocationId { get; init; }

    public required DateTimeOffset StartAtUtc { get; init; }

    public required DateTimeOffset EndAtUtc { get; init; }

    public required string TimeZoneId { get; init; }

    public string? RecurrenceRule { get; init; }

    public bool IsSeriesScope { get; init; }
}