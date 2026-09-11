namespace Unified.UserManagement.Models;

public sealed record ExpireLeaveRequestDto
{
    public required int LeaveId { get; init; }

    public required string ExpiryReason { get; init; }
}
