using System;

namespace Unified.UserManagement.Models;

public sealed record RoleDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public uint ConcurrencyToken { get; set; }
}
