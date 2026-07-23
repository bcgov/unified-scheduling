namespace Unified.UserManagement.Models;

public sealed record AwayLocationResponseDto
{
    public int Id { get; init; }
    public int EventId { get; init; }
    public Guid UserId { get; init; }
    public int LocationId { get; init; }
    public string LocationName { get; init; } = string.Empty;
    public string LocationTimezone { get; init; } = string.Empty;
    public DateTimeOffset StartAtUtc { get; init; }
    public DateTimeOffset? EndAtUtc { get; init; }
    public bool AllDay { get; init; }
    public DateTimeOffset? ExpiryAtUtc { get; init; }
    public string? ExpiryReason { get; init; }
    public string? Comment { get; init; }

    /// <summary>
    /// The timezone used when interpreting the stored UTC datetimes (away location's timezone).
    /// </summary>
    public string? Timezone { get; init; }
}
