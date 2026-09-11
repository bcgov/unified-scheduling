namespace Unified.UserManagement.Models;

public sealed record LeaveResponseDto
{
    public required int Id { get; init; }
    public required int EventId { get; init; }
    public required Guid UserId { get; init; }
    public required int LeaveTypeId { get; init; }
    public required string LeaveTypeCode { get; init; }
    public required string LeaveTypeDescription { get; init; }

    /// <summary>
    /// Whether time on this leave type is paid. Drives overtime calculations.
    /// </summary>
    public bool IsPaid { get; init; }

    public DateTimeOffset StartAtUtc { get; init; }
    public DateTimeOffset? EndAtUtc { get; init; }
    public bool AllDay { get; init; }
    public DateTimeOffset? ExpiryAtUtc { get; init; }
    public string? ExpiryReason { get; init; }
    public string? Comment { get; init; }

    /// <summary>
    /// The timezone used when interpreting the stored UTC datetimes.
    /// </summary>
    public string? Timezone { get; init; }
}
