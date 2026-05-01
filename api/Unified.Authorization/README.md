# Unified.Authorization

Permission-based authorization for the Unified Scheduling API using ASP.NET Core's built-in Policy framework.

## How it works

1. **Roles** are loaded from the user's record in the application database after Keycloak authentication and added as standard `ClaimTypes.Role` claims on the principal.
2. **`PermissionClaimsTransformer`** runs once per authenticated request. It reads the user's role claims, looks up the permissions associated with those roles, and adds `unified/permission` claims to the identity. The DB-backed lookup is pending; the transformer currently leaves the permission set empty and is the single integration point for `IPermissionService`.
3. **Named policies** are registered for every permission constant (e.g., `Permission:ShiftsEdit`).
4. **`PermissionAuthorizationHandler`** evaluates those policies by checking whether the user holds the matching permission claim. If the claim is absent it throws `UnauthorizedAccessException`, which the global exception handler maps to a `403 ProblemDetails` response.

## Securing a controller

Apply `[Authorize(Policy = ...)]` at the controller or action level. Because C# attribute arguments must be **compile-time constants**, you cannot call `AuthorizationModule.BuildPolicyName()` (a method) inside an attribute. Use string concatenation of the two `const` values instead:

```csharp
using Microsoft.AspNetCore.Authorization;
using Unified.Authorization;

[ApiController]
[Route("api/[controller]")]
public class ShiftsController(IShiftService shiftService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = AuthorizationModule.PolicyPrefix + Permissions.ShiftsView)]
    public async Task<ActionResult<IEnumerable<ShiftResponse>>> Get(...) { ... }

    [HttpPost]
    [Authorize(Policy = AuthorizationModule.PolicyPrefix + Permissions.ShiftsCreateAndAssign)]
    public async Task<ActionResult<ShiftResponse>> Create(...) { ... }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationModule.PolicyPrefix + Permissions.ShiftsEdit)]
    public async Task<ActionResult<ShiftResponse>> Update(...) { ... }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationModule.PolicyPrefix + Permissions.ShiftsExpire)]
    public async Task<IActionResult> Delete(...) { ... }
}
```

Use `AuthorizationModule.BuildPolicyName(permission)` at **runtime** (e.g., `IAuthorizationService.AuthorizeAsync`) where a method call is valid.

## Permission constants

All permission constants live in `Permissions.cs` and follow the `EntityNameAction` PascalCase convention (e.g., `ShiftsView`, `UsersCreate`).

## Adding a new permission

1. Add a constant to `Permissions.cs` following the `EntityNameAction` convention.
2. Register the policy in `AuthorizationModule.cs` (one `.AddPermissionPolicy(Permissions.YourNew)` line).
3. Apply `[Authorize(Policy = AuthorizationModule.PolicyPrefix + Permissions.YourNew)]` to the relevant controller or action.
4. Associate the permission with the appropriate role(s) in the database so `PermissionClaimsTransformer` will surface it for users in those roles.

## Replacing hardcoded data with database values

`PermissionClaimsTransformer` currently returns an empty permission set. It is marked with `// TODO` comments pointing to this section.

**Migration path:**

1. Add a `Permission` entity and a `RolePermission` join entity to `db/Models/UserManagement/`.
2. Create and run a migration to seed the initial data.
3. Create `IPermissionService` (e.g., in `Unified.UserManagement`) with a method:
   ```csharp
   Task<IReadOnlyList<string>> GetPermissionsForRolesAsync(IEnumerable<string> roles);
   ```
4. Inject `IPermissionService` into `PermissionClaimsTransformer` and populate the permission claims from the DB call.

The `AuthorizationModule`, policies, handler, and `RequirePermission` extension require **no changes** — only the data source changes.

## Improvements over sheriff-scheduling

This project replaces the [sheriff-scheduling authorization pattern](https://github.com/bcgov/sheriff-scheduling/tree/master/api/infrastructure/authorization) with standard ASP.NET Core primitives.

| Area | Sheriff Scheduling (legacy) | Unified Scheduling |
|---|---|---|
| Mechanism | Custom `IAuthorizationFilter` attribute | `IAuthorizationRequirement` + `IAuthorizationHandler` |
| API compatibility | MVC controllers only | MVC controllers (same pattern, standard ASP.NET Core) |
| DB coupling | `ClaimsService` directly queries EF in the transformer | Transformer is stateless; DB plugged in later via `IPermissionService` |
| Responsibility split | Auth logic spread across attribute, service, and transformer | Single path: transformer adds claims, handler evaluates one requirement |
| Idempotency | `_isTransformed` instance flag (scope bug risk with DI) | Checks for existing permission claims — stateless and safe |

## Registration

Called from `Program.cs`:

```csharp
builder.Services.AddAuthorizationModule();
```
