using Unified.Common.Seeding;
using Unified.Db.Models.UserManagement;
using Unified.UserManagement.Seeders;

namespace Unified.UserManagement;

public sealed class PlatformSystemUserSeedData : ISeedData<UserSeedDefinition>
{
    public static ISeedData<UserSeedDefinition> Instance { get; } = new PlatformSystemUserSeedData();

    private PlatformSystemUserSeedData() { }

    public IReadOnlyList<UserSeedDefinition> Definitions { get; } =
    [
        new()
        {
            Id = User.SystemUser,
            IdirName = "SYSTEM",
            IsEnabled = false,
            FirstName = "System",
            LastName = "System",
            BadgeNumber = "SYSTEM",
        },
    ];
}
