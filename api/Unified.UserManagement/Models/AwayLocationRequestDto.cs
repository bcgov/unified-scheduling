namespace Unified.UserManagement.Models;

public sealed record AwayLocationRequestDto
{
    public required int LocationId { get; init; }

    /// <summary>
    /// ISO 8601 datetime with UTC offset (e.g. 2026-01-10T08:30:00.000-08:00).
    /// Frontend computes the offset from the selected location's timezone. Backend stores as UTC.
    /// Use midnight (T00:00:00.000±HH:mm) for full-day away locations.
    /// </summary>
    public required string StartDateTime { get; init; }

    /// <summary>
    /// ISO 8601 datetime with UTC offset. Must be after StartDateTime.
    /// Use midnight (T00:00:00.000±HH:mm) for full-day away locations.
    /// </summary>
    public required string EndDateTime { get; init; }

    /// <summary>
    /// IANA timezone ID used to interpret StartDateTime and EndDateTime.
    /// Falls back to the away location's timezone when not provided.
    /// </summary>
    public string? Timezone { get; init; }

    public string? Comment { get; init; }
}
