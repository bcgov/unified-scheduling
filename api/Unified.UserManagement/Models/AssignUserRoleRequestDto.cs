namespace Unified.UserManagement.Models;

public sealed record AssignUserRoleRequestDto
{
    public required int RoleId { get; init; }

    public required DateTimeOffset EffectiveDate { get; init; }

    public DateTimeOffset? ExpiryDate { get; init; }
}
