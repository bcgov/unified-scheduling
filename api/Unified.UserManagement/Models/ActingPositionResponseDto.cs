namespace Unified.UserManagement.Models;

public sealed record ActingPositionResponseDto
{
    public int Id { get; init; }
    public Guid UserId { get; init; }
    public string PositionTypeCode { get; init; } = string.Empty;
    public string PositionTypeDescription { get; init; } = string.Empty;
    public DateTimeOffset StartAtUtc { get; init; }
    public DateTimeOffset? EndAtUtc { get; init; }
    public DateTimeOffset? ExpiryAtUtc { get; init; }
    public string? ExpiryReason { get; init; }
    public string? Comment { get; init; }

    /// <summary>
    /// The timezone used when interpreting the stored UTC datetimes (user's home location timezone).
    /// </summary>
    public string? Timezone { get; init; }
}
