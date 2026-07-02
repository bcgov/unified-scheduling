namespace Unified.Scheduling.Models;

public sealed record ShiftEntryRequest
{
    public int? ShiftSeriesId { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public string? Notes { get; init; }

    public string? Color { get; init; }

    public required DateTimeOffset StartAtUtc { get; init; }

    public DateTimeOffset? EndAtUtc { get; init; }

    public DateTimeOffset? SeriesStartAtUtc { get; init; }

    public DateTimeOffset? SeriesEndAtUtc { get; init; }

    public string? TimeZoneId { get; init; }

    public bool AllDay { get; init; }

    public bool IsException { get; init; }

    public string? StatusTypeCode { get; init; }

    public int? LocationId { get; init; }

    public IReadOnlyCollection<Guid> UserIds { get; init; } = [];
}
