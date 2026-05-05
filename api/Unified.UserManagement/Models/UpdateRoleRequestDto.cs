namespace Unified.UserManagement.Models;

public sealed record UpdateRoleRequestDto
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public IReadOnlyList<string> PermissionIds { get; init; } = [];

    public required uint ConcurrencyToken { get; init; }
}
