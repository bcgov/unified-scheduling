using Unified.Common.Seeding;

namespace Unified.Training.Seeders;

public sealed record TrainingProfileTypeSeedConfiguration : ISeedConfiguration<TrainingProfileTypeSeedDefinition>
{
    public required string Source { get; init; }

    public required IReadOnlyList<TrainingProfileTypeSeedDefinition> Definitions { get; init; }
}

public sealed record TrainingProfileTypeSeedDefinition
{
    public required string Code { get; init; }

    public required string Name { get; init; }
}
