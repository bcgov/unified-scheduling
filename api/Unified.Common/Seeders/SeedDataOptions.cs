namespace Unified.Common.Seeding;

public sealed class SeedDataOptions
{
    public const string SectionName = "SeedData";

    public string[] DataSets { get; set; } = [];
}

public sealed record ResolvedSeedDataConfiguration(IReadOnlyList<string> DataSets);
