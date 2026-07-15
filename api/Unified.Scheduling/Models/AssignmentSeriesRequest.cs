namespace Unified.Scheduling.Models;

public sealed record AssignmentSeriesRequest
{
    public int AssignmentDefinitionId { get; init; }
    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? Notes { get; init; }

    public string? Color { get; init; }

    public string? RecurrenceRule { get; init; }

    public string? TimeZoneId { get; init; }

    public DateTimeOffset StartAtUtc { get; init; }

    public DateTimeOffset? EndAtUtc { get; init; }

    public bool AllDay { get; init; }

    public int? LocationId { get; init; }

    public int? Capacity { get; init; }

    public IReadOnlyCollection<int>? ShiftSeriesIds { get; init; }

    public IReadOnlyCollection<Guid>? AssignedUserIds { get; init; }

    public IReadOnlyCollection<ShiftSeriesLinkRequest>? ShiftSeriesLinks { get; init; }
}
