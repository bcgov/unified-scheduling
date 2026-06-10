namespace Unified.UserManagement.Models;

public sealed record ExpireUserRoleRequestDto
{
    public required int RoleId { get; init; }

    public required string ExpiryReason { get; init; }
}
