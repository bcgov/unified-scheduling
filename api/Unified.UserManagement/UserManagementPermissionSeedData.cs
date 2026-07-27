using Unified.Authorization;
using Unified.Authorization.Seeders;
using Unified.Common.Seeding;
using Unified.UserManagement.Seeders;

namespace Unified.UserManagement;

/// <summary>
/// Permission seed data owned by the UserManagement module.
/// </summary>
public sealed class UserManagementPermissionSeedData : ISeedData<PermissionSeedDefinition>
{
    private const string PermissionGroupUsers = "Users";
    private const string PermissionGroupRoles = "Roles";
    private const string PermissionGroupActingPositions = "ActingPositions";
    private const string PermissionGroupAdmin = "Admin";
    public static ISeedData<PermissionSeedDefinition> Instance { get; } = new UserManagementPermissionSeedData();

    private UserManagementPermissionSeedData() { }

    public IReadOnlyList<PermissionSeedDefinition> Definitions { get; } =
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
    ];
}
