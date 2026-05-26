using Unified.Authorization;
using Unified.Common.Seeding;

namespace Unified.UserManagement;

/// <summary>
/// Static permission seed data owned by the UserManagement module.
/// </summary>
public static class UserManagementPermissionSeedData
{
    public static PermissionSeedConfiguration Configuration { get; } = new()
    {
        Permissions =
        [
            // Users
            new() { Group = "Users", Id = nameof(Permissions.UsersCreate), Description = "Create new users" },
            new() { Group = "Users", Id = nameof(Permissions.UsersEdit), Description = "Edit existing users" },
            new() { Group = "Users", Id = nameof(Permissions.UsersView), Description = "View users" },
            new() { Group = "Users", Id = nameof(Permissions.UsersExpire), Description = "Expire users" },
            new() { Group = "Users", Id = nameof(Permissions.UsersViewOtherProfiles), Description = "View other user profiles" },
            // Roles
            new() { Group = "Roles", Id = nameof(Permissions.RolesView), Description = "View roles" },
            new() { Group = "Roles", Id = nameof(Permissions.RolesCreateAndAssign), Description = "Create and Assign Roles" },
            new() { Group = "Roles", Id = nameof(Permissions.RolesEdit), Description = "Edit roles" },
            new() { Group = "Roles", Id = nameof(Permissions.RolesExpire), Description = "Expire roles" },
        ],
    };
}
