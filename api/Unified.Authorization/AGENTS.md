# AGENTS — Unified.Authorization

This file guides AI agents extending or working with the authorization system.

## Project purpose

`Unified.Authorization` provides permission-based access control using ASP.NET Core Authorization Policies. It is intentionally decoupled from the database — the role-to-permission mapping is currently hardcoded with clear TODO markers pointing to the migration path.

See [README.md § Improvements over sheriff-scheduling](README.md#improvements-over-sheriff-scheduling) for a comparison with the legacy approach.

## File map

| File | Responsibility |
|---|---|
| `Permissions.cs` | All permission name constants (`EntityNameAction` PascalCase) |
| `Roles.cs` | All role name constants (must match Keycloak) |
| `Claims/UnifiedClaimTypes.cs` | Custom claim type string used for permission claims |
| `Claims/PermissionClaimsTransformer.cs` | Adds permission claims from role claims at login |
| `Requirements/PermissionRequirement.cs` | `IAuthorizationRequirement` holding a single permission |
| `Requirements/PermissionAuthorizationHandler.cs` | Evaluates the requirement; throws `UnauthorizedAccessException` on failure |
| `AuthorizationModule.cs` | DI registration: transformer, handler, all named policies |
| `EndpointAuthorizationExtensions.cs` | `.RequirePermission(...)` fluent helper for Minimal API endpoints |

## Common tasks

### Add a permission for a new feature

1. `Permissions.cs` — add `public const string EntityNameAction = nameof(EntityNameAction);` following the existing naming convention.
2. `PermissionClaimsTransformer.cs` — add the constant to the appropriate role array(s).
3. `AuthorizationModule.cs` — add `.AddPermissionPolicy(Permissions.EntityNameAction)` inside `AddAuthorizationModule`.
4. Apply the policy to the controller or action (see **Protect a controller action** below).

### Add a new role

1. `Roles.cs` — add a `public const string YourRole = nameof(YourRole);`.
2. `PermissionClaimsTransformer.cs` — add a new `switch` arm with the role's permission array.
3. Ensure the role name exactly matches the Keycloak role name (case-sensitive).

### Protect a controller action

> **Important — use string concatenation, not `PolicyName()`, in attributes.**
>
> C# attribute arguments must be **compile-time constants**. `AuthorizationModule.PolicyName()` is a method call and cannot be used inside `[Authorize(...)]`. Instead, concatenate the two `const` values directly — the compiler evaluates it at compile time:
>
> ```csharp
> // ✅ Correct — both sides are const, result is a compile-time constant
> [Authorize(Policy = AuthorizationModule.PolicyPrefix + Permissions.ShiftsEdit)]
>
> // ❌ Wrong — method calls are not allowed in attribute arguments
> [Authorize(Policy = AuthorizationModule.PolicyName(Permissions.ShiftsEdit))]
> ```
>
> Use `AuthorizationModule.PolicyName(permission)` only at **runtime**, e.g., in `IAuthorizationService.AuthorizeAsync` calls inside service code.

Example controller:

```csharp
[HttpPut("{id:guid}")]
[Authorize(Policy = AuthorizationModule.PolicyPrefix + Permissions.ShiftsEdit)]
public async Task<ActionResult<ShiftResponse>> Update(Guid id, ...)
```

### Check permissions in service code

Inject `IAuthorizationService` and use the helper method (valid here because it's a runtime call, not an attribute):

```csharp
var result = await _authorizationService.AuthorizeAsync(
    user, null, AuthorizationModule.PolicyName(Permissions.ShiftsView));
if (!result.Succeeded) return Forbid();
```

### Replace static data with database

See `README.md` → "Replacing hardcoded data with database values" for the full migration path. The key integration point is `PermissionClaimsTransformer.TransformAsync` — replace the hardcoded array lookup with an async `IPermissionService` call.

## Design rules

- **Permission naming** — use `EntityNameAction` PascalCase (e.g., `ShiftsEdit`, `UsersCreate`). No underscores.
- **Policy naming** — policies are prefixed `"Permission:"` (e.g., `"Permission:ShiftsEdit"`). The prefix is intentional:
  - Makes authorization failures in logs immediately recognisable as permission checks rather than role or feature-flag policies.
  - Allows other policy families (`"FeatureFlag:"`) to coexist without name collisions.
  - The prefix is hidden from callers via `PolicyPrefix` constant and `PolicyName()` helper — no maintenance burden.
  > **Keep the prefix. Do not remove it.**
- **One requirement type** (`PermissionRequirement`) covers all cases. Do not create separate requirement classes per feature.
- **No logic in `Permissions.cs` or `Roles.cs`** — they are pure constant containers.
- **`PermissionClaimsTransformer` must stay idempotent** — it checks for existing permission claims before adding to prevent double-application.
- **Keep this project free of EF/DB dependencies** — data sourcing belongs in a service injected via interface (future state).
