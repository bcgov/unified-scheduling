namespace Unified.Common.Seeding;

public sealed record UserSeedConfiguration
{
    public required string Source { get; init; }

    public required IReadOnlyList<UserSeedDefinition> Users { get; init; }
}

public sealed record UserSeedDefinition
{
    public required Guid Id { get; init; }

    public required string IdirName { get; init; }

    public required bool IsEnabled { get; init; }

    public required string FirstName { get; init; }

    public required string LastName { get; init; }
}
