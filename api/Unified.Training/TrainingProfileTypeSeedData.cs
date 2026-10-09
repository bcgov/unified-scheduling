using Unified.Common.Seeding;
using Unified.Training.Seeders;

namespace Unified.Training;

public sealed class TrainingProfileTypeSeedData : ISeedData<TrainingProfileTypeSeedDefinition>
{
    public static ISeedData<TrainingProfileTypeSeedDefinition> Instance { get; } = new TrainingProfileTypeSeedData();

    private TrainingProfileTypeSeedData() { }

    public IReadOnlyList<TrainingProfileTypeSeedDefinition> Definitions { get; } =
    [new() { Code = "CARBINE_OPERATOR", Name = "Carbine Operator" }];
}
