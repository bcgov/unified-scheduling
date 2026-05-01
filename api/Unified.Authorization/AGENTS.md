# AGENTS — Unified.Authorization

This file guides AI agents extending or working with the authorization system.

## Project purpose

`Unified.Authorization` provides permission-based access control using ASP.NET Core Authorization Policies. The role-to-permission mapping is sourced from the application database via `PermissionClaimsTransformer` (DB-backed lookup is the pending integration point).

See [README.md § Improvements over sheriff-scheduling](README.md#improvements-over-sheriff-scheduling) for a comparison with the legacy approach.

## File map

| File | Responsibility |
|---|---|
| `Permissions.cs` | All permission name constants (`EntityNameAction` PascalCase) |
| `Roles.cs` | Role name constants (kept in sync with role records loaded from the application DB) |
| `Claims/UnifiedClaimTypes.cs` | Custom claim type string used for permission claims |
| `Claims/PermissionClaimsTransformer.cs` | Adds permission claims to the principal based on the user's role claims |
| `Requirements/PermissionRequirement.cs` | `IAuthorizationRequirement` holding a single permission |
| `Requirements/PermissionAuthorizationHandler.cs` | Evaluates the requirement; throws `ForbiddenException` on failure |
| `AuthorizationModule.cs` | DI registration: transformer, handler, all named policies |
| `EndpointAuthorizationExtensions.cs` | `.RequirePermission(...)` fluent helper for Minimal API endpoints |

## Common tasks

### Add a permission for a new feature

1. `Permissions.cs` — add `public const string EntityNameAction = nameof(EntityNameAction);` following the existing naming convention.
2. In your feature module's `AddXxxModule` method, call `services.AddAuthorizationBuilder().AddPermissionPolicy(Permissions.EntityNameAction)`. Each module owns its own permission registrations — do not add them to `AuthorizationModule`.
3. Apply the policy to the controller or action (see **Protect a controller action** below).
4. Associate the permission with the appropriate role(s) in the database so the transformer surfaces it on the principal.

### Add a new role

1. `Roles.cs` — add a `public const string YourRole = nameof(YourRole);`.
2. Insert the corresponding role record (and its permission associations) in the application database.
3. Ensure the role name in `Roles.cs` exactly matches the DB role name (case-sensitive).

### Protect a controller action

> **Important — use string concatenation, not `BuildPolicyName()`, in attributes.**
>
> C# attribute arguments must be **compile-time constants**. `AuthorizationModule.BuildPolicyName()` is a method call and cannot be used inside `[Authorize(...)]`. Instead, concatenate the two `const` values directly — the compiler evaluates it at compile time:
>
> ```csharp
> // ✅ Correct — both sides are const, result is a compile-time constant
> [Authorize(Policy = AuthorizationModule.PolicyPrefix + Permissions.ShiftsEdit)]
>
> // ❌ Wrong — method calls are not allowed in attribute arguments
> [Authorize(Policy = AuthorizationModule.BuildPolicyName(Permissions.ShiftsEdit))]
> ```
>
> Use `AuthorizationModule.BuildPolicyName(permission)` only at **runtime**, e.g., in `IAuthorizationService.AuthorizeAsync` calls inside service code.

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
    user, null, AuthorizationModule.BuildPolicyName(Permissions.ShiftsView));
if (!result.Succeeded) return Forbid();
```

### Replace static data with database

See `README.md` → "Replacing hardcoded data with database values" for the full migration path. The key integration point is `PermissionClaimsTransformer.TransformAsync` — inject `IPermissionService` and populate permission claims from the DB.

## Design rules

- **Permission naming** — use `EntityNameAction` PascalCase (e.g., `ShiftsEdit`, `UsersCreate`). No underscores.
- **Policy naming** — policies are prefixed `"Permission:"` (e.g., `"Permission:ShiftsEdit"`). The prefix is intentional:
  - Makes authorization failures in logs immediately recognisable as permission checks rather than role or feature-flag policies.
  - Allows other policy families (`"FeatureFlag:"`) to coexist without name collisions.
  - The prefix is hidden from callers via `PolicyPrefix` constant and `BuildPolicyName()` helper — no maintenance burden.
  > **Keep the prefix. Do not remove it.**
- **One requirement type** (`PermissionRequirement`) covers all cases. Do not create separate requirement classes per feature.
- **No logic in `Permissions.cs` or `Roles.cs`** — they are pure constant containers.
- **`PermissionClaimsTransformer` must stay idempotent** — it checks for existing permission claims before adding to prevent double-application.
- **Keep this project free of EF/DB dependencies** — data sourcing belongs in a service injected via interface (future state).
