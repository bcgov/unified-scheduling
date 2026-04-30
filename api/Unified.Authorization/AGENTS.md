# AGENTS — Unified.Authorization

This file guides AI agents extending or working with the authorization system.

## Project purpose

`Unified.Authorization` provides permission-based access control using ASP.NET Core Authorization Policies. It is intentionally decoupled from the database — all data is currently hardcoded with clear TODO markers pointing to the migration path.

See [README.md § Improvements over sheriff-scheduling](README.md#improvements-over-sheriff-scheduling) for a comparison with the legacy approach.

## File map

| File | Responsibility |
|---|---|
| `Permissions.cs` | All permission name constants |
| `Roles.cs` | All role name constants (must match Keycloak) |
| `Claims/UnifiedClaimTypes.cs` | Custom claim type string used for permission claims |
| `Claims/PermissionClaimsTransformer.cs` | Adds permission claims from role claims at login |
| `Requirements/PermissionRequirement.cs` | `IAuthorizationRequirement` holding a single permission |
| `Requirements/PermissionAuthorizationHandler.cs` | Evaluates the requirement against claims |
| `AuthorizationModule.cs` | DI registration: transformer, handler, all named policies |
| `EndpointAuthorizationExtensions.cs` | `.RequirePermission(...)` fluent helper for endpoints |

## Common tasks

### Add a permission for a new feature

1. `Permissions.cs` — add a `public const string YourPermission = nameof(YourPermission);`
2. `PermissionClaimsTransformer.cs` — add the constant to the appropriate role array(s) in `GetPermissionsForRole()`
3. `AuthorizationModule.cs` — add `.AddPermissionPolicy(Permissions.YourPermission)` inside `AddAuthorizationModule`
4. Apply `[Authorize(Policy = AuthorizationModule.PolicyName(Permissions.YourPermission))]` to the controller or action

### Add a new role

1. `Roles.cs` — add a `public const string YourRole = nameof(YourRole);`
2. `PermissionClaimsTransformer.cs` — add a new `switch` arm to `GetPermissionsForRole()` returning the role's permissions
3. Ensure the role name exactly matches the Keycloak role name (case-sensitive)

### Protect a controller action

Use the standard `[Authorize(Policy = ...)]` attribute on the controller class or individual actions:

```csharp
// Controller-level: all actions require Login
[Authorize(Policy = AuthorizationModule.PolicyName(Permissions.Login))]
public class ShiftsController : ControllerBase
{
    // Action-level: additional permission on top of Login
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationModule.PolicyName(Permissions.EditShifts))]
    public async Task<ActionResult<ShiftResponse>> Update(Guid id, ...)
```

Always use `AuthorizationModule.PolicyName(...)` — never write the policy name string by hand.

### Check permissions in service code

Inject `IAuthorizationService` and use:

```csharp
var result = await _authorizationService.AuthorizeAsync(
    user, null, AuthorizationModule.PolicyName(Permissions.ViewShifts));
if (!result.Succeeded) return Forbid();
```

### Replace static data with database

See `README.md` → "Replacing hardcoded data with database values" for the full migration path. The key integration point is `PermissionClaimsTransformer.TransformAsync` — replace the hardcoded `GetPermissionsForRole` call with an async service call.

## Design rules

- **One requirement type** (`PermissionRequirement`) covers all cases. Do not create separate requirement classes per feature.
- **No logic in `Permissions.cs` or `Roles.cs`** — they are pure constant containers.
- **Policies are named** `"Permission:{PermissionName}"` — always use `AuthorizationModule.PolicyName(...)` to build the string.
- **`PermissionClaimsTransformer` must stay idempotent** — it checks for existing permission claims before adding to prevent double-application.
- **Keep this project free of EF/DB dependencies** — data sourcing belongs in a service injected via interface (future state).
