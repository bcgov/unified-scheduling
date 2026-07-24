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
    private const string PermissionGroupActingPositions = "ActingPositions";
    private const string PermissionGroupAdmin = "Admin";
    private const string PermissionGroupAwayLocations = "AwayLocations";
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
                // Acting Positions
                new()
                {
                    Group = PermissionGroupActingPositions,
                    Id = nameof(Permissions.ActingPositionsView),
                    Description = "View acting positions",
                },
                new()
                {
                    Group = PermissionGroupActingPositions,
                    Id = nameof(Permissions.ActingPositionsCreate),
                    Description = "Create acting positions",
                },
                new()
                {
                    Group = PermissionGroupActingPositions,
                    Id = nameof(Permissions.ActingPositionsEdit),
                    Description = "Edit acting positions",
                },
                new()
                {
                    Group = PermissionGroupActingPositions,
                    Id = nameof(Permissions.ActingPositionsExpire),
                    Description = "Expire acting positions",
                },
                // Admin
                new()
                {
                    Group = PermissionGroupAdmin,
                    Id = nameof(Permissions.HangfireDashboardView),
                    Description = "View the Hangfire background jobs dashboard",
                },
                // Away Locations
                new()
                {
                    Group = PermissionGroupAwayLocations,
                    Id = nameof(Permissions.AwayLocationsView),
                    Description = "View away locations",
                },
                new()
                {
                    Group = PermissionGroupAwayLocations,
                    Id = nameof(Permissions.AwayLocationsCreate),
                    Description = "Create away locations",
                },
                new()
                {
                    Group = PermissionGroupAwayLocations,
                    Id = nameof(Permissions.AwayLocationsEdit),
                    Description = "Edit away locations",
                },
                new()
                {
                    Group = PermissionGroupAwayLocations,
                    Id = nameof(Permissions.AwayLocationsExpire),
                    Description = "Expire away locations",
                },
            ],
        };
}
