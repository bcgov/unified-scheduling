# Unified.Authorization

Permission-based authorization for the Unified Scheduling API using ASP.NET Core's built-in Policy framework.

## How it works

1. **Roles** come from Keycloak as standard `ClaimTypes.Role` claims.
2. **`PermissionClaimsTransformer`** runs after authentication. It reads the user's role claims, expands them into permission claims, and adds `unified/permission` claims to the identity.
3. **Named policies** are registered for every permission constant (e.g., `Permission:EditShifts`).
4. **`PermissionAuthorizationHandler`** evaluates those policies by checking whether the user holds the matching permission claim.

## Securing a controller

Apply `[Authorize(Policy = ...)]` at the controller or action level using `AuthorizationModule.PolicyName()` to avoid magic strings.

```csharp
using Microsoft.AspNetCore.Authorization;
using Unified.Authorization;

// Entire controller requires Login
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AuthorizationModule.PolicyName(Permissions.Login))]
public class ShiftsController(IShiftService shiftService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = AuthorizationModule.PolicyName(Permissions.ViewShifts))]
    public async Task<ActionResult<IEnumerable<ShiftResponse>>> Get(...) { ... }

    [HttpPost]
    [Authorize(Policy = AuthorizationModule.PolicyName(Permissions.CreateAndAssignShifts))]
    public async Task<ActionResult<ShiftResponse>> Create(...) { ... }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationModule.PolicyName(Permissions.EditShifts))]
    public async Task<ActionResult<ShiftResponse>> Update(...) { ... }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationModule.PolicyName(Permissions.ExpireShifts))]
    public async Task<IActionResult> Delete(...) { ... }
}
```

## Available permissions

All permission constants live in `Permissions.cs`. Roles and their default permission grants are currently hardcoded in `PermissionClaimsTransformer.GetPermissionsForRole()`.

| Category | Permissions |
|---|---|
| Auth | `Login` |
| Users | `CreateUsers`, `EditUsers`, `ViewUsers`, `ExpireUsers` |
| Roles | `ViewRoles`, `CreateAndAssignRoles`, `EditRoles`, `ExpireRoles` |
| Shifts | `ViewShifts`, `CreateAndAssignShifts`, `EditShifts`, `ExpireShifts`, `ImportShifts` |
| Duties | `ViewDutyRoster`, `CreateAndAssignDuties`, `EditDuties`, `ExpireDuties` |
| Location | `ViewHomeLocation`, `ViewAssignedLocation`, `ViewRegion`, `ViewProvince` |
| Reports | `GenerateReports` |

## Adding a new permission

1. Add a constant to `Permissions.cs`.
2. Add it to the appropriate role array in `PermissionClaimsTransformer.GetPermissionsForRole()`.
3. Register the policy in `AuthorizationModule.cs` (one `.AddPermissionPolicy(Permissions.YourNew)` line).
4. Apply `[Authorize(Policy = AuthorizationModule.PolicyName(Permissions.YourNew))]` to the relevant controller or action.

## Replacing hardcoded data with database values

The roles, permissions, and their mappings are currently hardcoded in `PermissionClaimsTransformer.GetPermissionsForRole()`. They are marked with `// TODO` comments pointing to this section.

**Migration path:**

1. Add a `Permission` entity and a `RolePermission` join entity to `db/Models/UserManagement/`.
2. Create and run a migration to seed the initial data from the hardcoded role-permission mappings.
3. Create `IPermissionService` (e.g., in `Unified.UserManagement`) with a method:
   ```csharp
   Task<IReadOnlyList<string>> GetPermissionsForRolesAsync(IEnumerable<string> roles);
   ```
4. Inject `IPermissionService` into `PermissionClaimsTransformer` and replace the hardcoded `GetPermissionsForRole` call with the async DB call.
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

This replaces the bare `builder.Services.AddAuthorization()` call.
