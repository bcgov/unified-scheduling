namespace Unified.Common.Seeding;

/// <summary>
/// Shared configuration contract for permission seed entries.
/// Modules can construct this and register it in DI for permission seeders.
/// </summary>
public sealed record PermissionSeedConfiguration(string Source, IReadOnlyList<PermissionSeedDefinition> Permissions);

/// <summary>
/// Database-agnostic permission seed definition.
/// </summary>
public sealed record PermissionSeedDefinition(string Id, string Description);
