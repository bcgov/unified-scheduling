using Unified.Common.Seeding;

namespace Unified.UserManagement.Seeders;

public sealed record LocationSeedConfiguration : ISeedConfiguration<LocationSeedDefinition>
{
    public required string Source { get; init; }

    public required IReadOnlyList<LocationSeedDefinition> Definitions { get; init; }
}

public sealed record LocationSeedDefinition
{
    public required int Id { get; init; }

    public required string AgencyId { get; init; }

    public required string Name { get; init; }

    public string? JustinCode { get; init; }

    public int? RegionId { get; init; }

    public required string Timezone { get; init; }
}