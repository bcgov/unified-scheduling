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
}
