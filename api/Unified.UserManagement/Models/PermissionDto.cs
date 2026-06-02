namespace Unified.UserManagement.Models;

public sealed record PermissionDto
{
    public required string Id { get; init; }

    public string Description { get; init; } = string.Empty;

    public string Group { get; init; } = string.Empty;

    public uint ConcurrencyToken { get; init; }
}
