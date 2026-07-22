namespace Unified.Common.Seeding;

public sealed record RegionSeedConfiguration
{
    public required string Source { get; init; }

    public required IReadOnlyList<RegionSeedDefinition> Regions { get; init; }
}

public sealed record RegionSeedDefinition
{
    public required int Id { get; init; }

    public int? JustinId { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }
}
