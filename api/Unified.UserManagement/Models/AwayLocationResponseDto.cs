namespace Unified.UserManagement.Models;

public sealed record AwayLocationResponseDto
{
    public required int Id { get; init; }
    public required int EventId { get; init; }
    public required Guid UserId { get; init; }
    public required int LocationId { get; init; }
    public required string LocationName { get; init; }
    public required string LocationTimezone { get; init; }
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
