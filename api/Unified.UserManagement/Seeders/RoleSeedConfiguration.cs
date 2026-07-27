using Unified.Common.Seeding;

namespace Unified.UserManagement.Seeders;

public sealed record RoleSeedConfiguration : ISeedConfiguration<RoleSeedDefinition>
{
    public required string Source { get; init; }

    public required IReadOnlyList<RoleSeedDefinition> Definitions { get; init; }
}

public sealed record RoleSeedDefinition
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }
}
