using Unified.Common.Seeding;

namespace Unified.Stats.Seeders;

public sealed record BiOracleLocationMappingSeedConfiguration
    : ISeedConfiguration<BiOracleLocationMappingSeedDefinition>
{
    public required string Source { get; init; }

    public required IReadOnlyList<BiOracleLocationMappingSeedDefinition> Definitions { get; init; }
}

public sealed record BiOracleLocationMappingSeedDefinition
{
    public required int Id { get; init; }

    public required string JustinLocationCode { get; init; }

    public required int CrtLocId { get; init; }

    public DateOnly EffectiveDate { get; init; } = new(1989, 1, 1);

    public DateOnly? ExpiryDate { get; init; }
}
