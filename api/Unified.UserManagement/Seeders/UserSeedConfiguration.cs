using Unified.Common.Seeding;

namespace Unified.UserManagement.Seeders;

public sealed record UserSeedConfiguration : ISeedConfiguration<UserSeedDefinition>
{
    public required string Source { get; init; }

    public required IReadOnlyList<UserSeedDefinition> Definitions { get; init; }
}

public sealed record UserSeedDefinition
{
    public required Guid Id { get; init; }

    public required string IdirName { get; init; }

    public required bool IsEnabled { get; init; }

    public required string FirstName { get; init; }

    public required string LastName { get; init; }
}