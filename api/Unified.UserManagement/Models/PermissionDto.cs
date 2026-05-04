namespace Unified.UserManagement.Models;

public sealed record PermissionDto
{
    public string Id { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public uint ConcurrencyToken { get; init; }
}
