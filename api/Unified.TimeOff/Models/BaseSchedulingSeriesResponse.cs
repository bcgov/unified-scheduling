namespace Unified.TimeOff.Models;

/// <summary>
/// Fields shared by responses describing a recurring series backed by a calendar <c>EventSeries</c>.
/// </summary>
public abstract record BaseSchedulingSeriesResponse
{
    public int Id { get; init; }

    public int EventSeriesId { get; init; }

    public string? Title { get; init; }

    public string? Description { get; init; }

    public string? Notes { get; init; }

    public string? Color { get; init; }

    public string? RecurrenceRule { get; init; }

    public string? TimeZoneId { get; init; }

    public DateTimeOffset? StartAtUtc { get; init; }

    public DateTimeOffset? EndAtUtc { get; init; }

    public bool AllDay { get; init; }

    public string? StatusTypeCode { get; init; }

    public int? LocationId { get; init; }

    public IReadOnlyCollection<int> EventIds { get; init; } = [];
}
