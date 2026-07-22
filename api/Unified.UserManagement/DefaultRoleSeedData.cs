using Unified.Common.Seeding;

namespace Unified.UserManagement;

public static class DefaultRoleSeedData
{
    public static IReadOnlyList<RoleSeedDefinition> Roles { get; } =
    [
        new()
        {
            Id = 1,
            Name = "Administrator",
            Description = "Administrator",
        },
        new()
        {
            Id = 2,
            Name = "Manager",
            Description = "Manager",
        },
        new()
        {
            Id = 3,
            Name = "Staff",
            Description = "Staff",
        },
    ];
}
