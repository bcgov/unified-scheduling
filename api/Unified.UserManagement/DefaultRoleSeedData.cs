using Unified.Common.Seeding;
using Unified.UserManagement.Seeders;

namespace Unified.UserManagement;

public sealed class DefaultRoleSeedData : ISeedData<RoleSeedDefinition>
{
    public static ISeedData<RoleSeedDefinition> Instance { get; } = new DefaultRoleSeedData();

    private DefaultRoleSeedData() { }

    public IReadOnlyList<RoleSeedDefinition> Definitions { get; } =
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
