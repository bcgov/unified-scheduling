using Unified.Common.Seeding;

namespace Unified.Stats.Seeders;

public sealed record BiOracleStatMappingSeedConfiguration : ISeedConfiguration<BiOracleStatMappingSetSeedDefinition>
{
    public required string Source { get; init; }

    public required IReadOnlyList<BiOracleStatMappingSetSeedDefinition> Definitions { get; init; }
}

public sealed record BiOracleStatMappingSetSeedDefinition
{
    public required int Id { get; init; }

    public DateOnly? EffectiveDate { get; init; }

    public DateOnly? ExpiryDate { get; init; }

    public required IReadOnlyList<BiOracleStatMappingSeedDefinition> Mappings { get; init; }
}

public sealed record BiOracleStatMappingSeedDefinition
{
    public required int Id { get; init; }

    public required int SubCategoryMetricId { get; init; }

    public required string TargetTable { get; init; }

    public required string TargetColumn { get; init; }
}
