using Unified.Authorization;

namespace Unified.LoanOut;

/// <summary>
/// Pre-built policy name constants for use in <c>[Authorize(Policy = ...)]</c> attributes
/// within the Loan Out module. Combines <see cref="AuthorizationModule.PolicyPrefix"/>
/// with each permission name so controllers never perform string concatenation.
/// </summary>
public static class LoanOutPolicies
{
    public const string LoanOutView = AuthorizationModule.PolicyPrefix + nameof(Permissions.LoanOutView);
    public const string LoanOutCreateAndAssign =
        AuthorizationModule.PolicyPrefix + nameof(Permissions.LoanOutCreateAndAssign);
    public const string LoanOutEdit = AuthorizationModule.PolicyPrefix + nameof(Permissions.LoanOutEdit);
    public const string LoanOutDelete = AuthorizationModule.PolicyPrefix + nameof(Permissions.LoanOutDelete);

    // Accept either the scheduling permission or the matching user (My Team) permission.
    public const string LoanOutViewOrUser = AuthorizationModule.PolicyPrefix + "LoanOutViewOrUser";
    public const string LoanOutCreateOrUser = AuthorizationModule.PolicyPrefix + "LoanOutCreateOrUser";
    public const string LoanOutEditOrUser = AuthorizationModule.PolicyPrefix + "LoanOutEditOrUser";
    public const string LoanOutDeleteOrUser = AuthorizationModule.PolicyPrefix + "LoanOutDeleteOrUser";

    // Loan Out types are a lookup used when recording loan out, so either loan out view permission grants access.
    public const string LoanOutTypesView = AuthorizationModule.PolicyPrefix + "LoanOutTypesView";
}
