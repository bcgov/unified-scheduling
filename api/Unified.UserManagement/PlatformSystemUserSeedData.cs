using Unified.Common.Seeding;
using Unified.Db.Models.UserManagement;

namespace Unified.UserManagement;

public static class PlatformSystemUserSeedData
{
    public static IReadOnlyList<UserSeedDefinition> Users { get; } =
    [
        new()
        {
            Id = User.SystemUser,
            IdirName = "SYSTEM",
            IsEnabled = false,
            FirstName = "System",
            LastName = "System",
        },
    ];
}
