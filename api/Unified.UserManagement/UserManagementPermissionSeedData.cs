using Unified.Authorization;
using Unified.Common.Seeding;

namespace Unified.UserManagement;

/// <summary>
/// Static permission seed data owned by the UserManagement module.
/// </summary>
public static class UserManagementPermissionSeedData
{
    private const string PermissionGroupUsers = "Users";
    private const string PermissionGroupRoles = "Roles";
    public static PermissionSeedConfiguration Configuration { get; } =
        new()
        {
            Permissions =
            [
                // Users
                new()
                {
                    Group = PermissionGroupUsers,
                    Id = nameof(Permissions.UsersCreate),
                    Description = "Create new users",
                },
                new()
                {
                    Group = PermissionGroupUsers,
                    Id = nameof(Permissions.UsersEdit),
                    Description = "Edit existing users",
                },                
                new()
                {
                    Group = PermissionGroupUsers,
                    Id = nameof(Permissions.UserRoleAssign),
                    Description = "Assign roles to users",
                },
                new()
                {
                    Group = PermissionGroupUsers,
                    Id = nameof(Permissions.UsersView),
                    Description = "View users",
                },
                new()
                {
                    Group = PermissionGroupUsers,
                    Id = nameof(Permissions.UsersExpire),
                    Description = "Expire users",
                },
                new()
                {
                    Group = PermissionGroupUsers,
                    Id = nameof(Permissions.UsersViewOtherProfiles),
                    Description = "View other user profiles",
                },
                // Roles
                new()
                {
                    Group = PermissionGroupRoles,
                    Id = nameof(Permissions.RolesView),
                    Description = "View roles",
                },
                new()
                {
                    Group = PermissionGroupRoles,
                    Id = nameof(Permissions.RolesCreate),
                    Description = "Create roles",
                },
                new()
                {
                    Group = PermissionGroupRoles,
                    Id = nameof(Permissions.RolesEdit),
                    Description = "Edit roles",
                },
                new()
                {
                    Group = PermissionGroupRoles,
                    Id = nameof(Permissions.RolesExpire),
                    Description = "Expire roles",
                },
            ],
        };
}
