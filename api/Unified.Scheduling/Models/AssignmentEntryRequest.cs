namespace Unified.Scheduling.Models;

public sealed record AssignmentEntryRequest
{
    public int? AssignmentSeriesId { get; init; }

    public int AssignmentDefinitionId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? Notes { get; init; }

    public string? Color { get; init; }

    public DateTimeOffset StartAtUtc { get; init; }

    public DateTimeOffset? EndAtUtc { get; init; }

    public DateTimeOffset? SeriesStartAtUtc { get; init; }

    public DateTimeOffset? SeriesEndAtUtc { get; init; }

    public string? TimeZoneId { get; init; }

    public bool AllDay { get; init; }

    public int? LocationId { get; init; }

    public int? Capacity { get; init; }

    public IReadOnlyCollection<int>? ShiftEntryIds { get; init; }

    public IReadOnlyCollection<Guid>? AssignedUserIds { get; init; }

    public IReadOnlyCollection<ShiftEntryLinkRequest>? ShiftEntryLinks { get; init; }
}
