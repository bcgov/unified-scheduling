using Unified.Common.Seeding;

namespace Unified.UserManagement.Seeders;

public sealed record RegionSeedConfiguration : ISeedConfiguration<RegionSeedDefinition>
{
    public required string Source { get; init; }

    public required IReadOnlyList<RegionSeedDefinition> Definitions { get; init; }
}

public sealed record RegionSeedDefinition
{
    public required int Id { get; init; }

    public int? JustinId { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }
}