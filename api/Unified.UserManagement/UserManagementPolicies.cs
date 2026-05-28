using Unified.Authorization;

namespace Unified.UserManagement;

/// <summary>
/// Pre-built policy name constants for use in <c>[Authorize(Policy = ...)]</c> attributes
/// within the UserManagement module. Combines <see cref="AuthorizationModule.PolicyPrefix"/>
/// with each permission name so controllers never perform string concatenation.
/// </summary>
public static class UserManagementPolicies
{
    // --- Users ---
    public const string UsersCreate = AuthorizationModule.PolicyPrefix + nameof(Permissions.UsersCreate);
    public const string UsersEdit = AuthorizationModule.PolicyPrefix + nameof(Permissions.UsersEdit);
    public const string UserRoleAssign = AuthorizationModule.PolicyPrefix + nameof(Permissions.UserRoleAssign);
    public const string UsersView = AuthorizationModule.PolicyPrefix + nameof(Permissions.UsersView);
    public const string UsersExpire = AuthorizationModule.PolicyPrefix + nameof(Permissions.UsersExpire);
    public const string UsersViewOtherProfiles =
        AuthorizationModule.PolicyPrefix + nameof(Permissions.UsersViewOtherProfiles);

    // --- Roles ---
    public const string RolesView = AuthorizationModule.PolicyPrefix + nameof(Permissions.RolesView);
    public const string RolesCreate = AuthorizationModule.PolicyPrefix + nameof(Permissions.RolesCreate);
    public const string RolesEdit = AuthorizationModule.PolicyPrefix + nameof(Permissions.RolesEdit);
    public const string RolesExpire = AuthorizationModule.PolicyPrefix + nameof(Permissions.RolesExpire);
}
