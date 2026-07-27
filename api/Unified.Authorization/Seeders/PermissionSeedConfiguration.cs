using Unified.Common.Seeding;

namespace Unified.Authorization.Seeders;

/// <summary>
/// Configuration contract for permission seed entries.
/// Modules can construct this and register it for the permission seeder.
/// </summary>
public sealed record PermissionSeedConfiguration : ISeedConfiguration<PermissionSeedDefinition>
{
    public required string Source { get; init; }

    public required IReadOnlyList<PermissionSeedDefinition> Definitions { get; init; }
}

/// <summary>
/// Database-agnostic permission seed definition.
/// </summary>
public sealed record PermissionSeedDefinition
{
    public required string Group { get; init; }

    public required string Id { get; init; }

    public required string Description { get; init; }
}