namespace Unified.Common.Seeding;

public sealed record RoleSeedConfiguration
{
    public required string Source { get; init; }

    public required IReadOnlyList<RoleSeedDefinition> Roles { get; init; }
}

public sealed record RoleSeedDefinition
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }
}
