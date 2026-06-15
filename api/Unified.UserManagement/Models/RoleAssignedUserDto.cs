namespace Unified.UserManagement.Models;

public sealed record RoleAssignedUserDto
{
    public Guid UserId { get; init; }

    public bool IsEnabled { get; init; }

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;
}