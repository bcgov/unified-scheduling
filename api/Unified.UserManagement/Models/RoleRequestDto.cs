namespace Unified.UserManagement.Models;

public sealed record RoleRequestDto
{
    public required string Name { get; init; }

    public required string Description { get; init; }
}
