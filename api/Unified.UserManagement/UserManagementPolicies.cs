using Unified.Authorization;

namespace Unified.UserManagement;

/// <summary>
/// Pre-built policy name constants for use in <c>[Authorize(Policy = ...)]</c> attributes
/// within the UserManagement module. Combines <see cref="AuthorizationModule.PolicyPrefix"/>
/// with each <see cref="Permissions"/> constant so controllers never perform string concatenation.
/// </summary>
public static class UserManagementPolicies
{
    // --- Users ---
    public const string UsersCreate = AuthorizationModule.PolicyPrefix + Permissions.UsersCreate;
    public const string UsersEdit = AuthorizationModule.PolicyPrefix + Permissions.UsersEdit;
    public const string UsersView = AuthorizationModule.PolicyPrefix + Permissions.UsersView;
    public const string UsersExpire = AuthorizationModule.PolicyPrefix + Permissions.UsersExpire;
    public const string UsersViewOtherProfiles = AuthorizationModule.PolicyPrefix + Permissions.UsersViewOtherProfiles;

    // --- Roles ---
    public const string RolesView = AuthorizationModule.PolicyPrefix + Permissions.RolesView;
    public const string RolesCreateAndAssign = AuthorizationModule.PolicyPrefix + Permissions.RolesCreateAndAssign;
    public const string RolesEdit = AuthorizationModule.PolicyPrefix + Permissions.RolesEdit;
    public const string RolesExpire = AuthorizationModule.PolicyPrefix + Permissions.RolesExpire;
}
