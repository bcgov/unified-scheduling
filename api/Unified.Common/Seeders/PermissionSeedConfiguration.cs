namespace Unified.Common.Seeding;

/// <summary>
/// Shared configuration contract for permission seed entries.
/// Modules can construct this and register it in DI for permission seeders.
/// </summary>
public sealed record PermissionSeedConfiguration
{
	public required IReadOnlyList<PermissionSeedDefinition> Permissions { get; init; }
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
