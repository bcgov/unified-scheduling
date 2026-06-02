namespace Unified.UserManagement.Models;

public sealed record UserRoleResponseDto
{
    public int Id { get; init; }
    public Guid UserId { get; init; }
    public int RoleId { get; init; }
    public DateTimeOffset EffectiveDate { get; init; }
    public DateTimeOffset? ExpiryDate { get; init; }
    public string? ExpiryReason { get; init; }
}
