# Unified.Authorization

Permission-based authorization for the Unified Scheduling API using ASP.NET Core's built-in Policy framework.

## How it works

1. **Roles** come from Keycloak as standard `ClaimTypes.Role` claims.
2. **`PermissionClaimsTransformer`** runs once per authenticated request. It reads the user's role claims, expands them into permission claims, and adds `unified/permission` claims to the identity. Currently the role-to-permission mapping is hardcoded; the transformer is designed to swap in a DB-backed service later.
3. **Named policies** are registered for every permission constant (e.g., `Permission:ShiftsEdit`).
4. **`PermissionAuthorizationHandler`** evaluates those policies by checking whether the user holds the matching permission claim. If the claim is absent it throws `UnauthorizedAccessException`, which the global exception handler maps to a `403 ProblemDetails` response.

## Securing a controller

Apply `[Authorize(Policy = ...)]` at the controller or action level. Because C# attribute arguments must be **compile-time constants**, you cannot call `AuthorizationModule.PolicyName()` (a method) inside an attribute. Use string concatenation of the two `const` values instead:

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

Use `AuthorizationModule.PolicyName(permission)` at **runtime** (e.g., `IAuthorizationService.AuthorizeAsync`) where a method call is valid.

## Available permissions

All permission constants live in `Permissions.cs` and follow the `EntityNameAction` PascalCase convention.

| Category | Permissions |
|---|---|
| Auth | `AuthLogin` |
| Users | `UsersCreate`, `UsersEdit`, `UsersView`, `UsersExpire`, `UsersViewOtherProfiles` |
| Roles | `RolesView`, `RolesCreateAndAssign`, `RolesEdit`, `RolesExpire` |
| Types | `TypesCreate`, `TypesEdit`, `TypesExpire` |
| Shifts | `ShiftsView`, `ShiftsCreateAndAssign`, `ShiftsEdit`, `ShiftsExpire`, `ShiftsImport`, `ShiftsViewAllFuture` |
| Schedule | `ScheduleViewDistribute` |
| Assignments | `AssignmentsCreate`, `AssignmentsEdit`, `AssignmentsExpire` |
| Duty Roster | `DutyRosterView`, `DutyRosterViewFuture` |
| Duties | `DutiesCreateAndAssign`, `DutiesEdit`, `DutiesExpire` |
| Location | `LocationViewHome`, `LocationViewAssigned`, `LocationViewRegion`, `LocationViewProvince`, `LocationExpire` |
| Training | `TrainingEditPast`, `TrainingRemovePast`, `TrainingAdjustExpiry`, `TrainingExempt` |
| IDIR | `IdirEdit` |
| Reports | `ReportsGenerate` |

## Adding a new permission

1. Add a constant to `Permissions.cs` following the `EntityNameAction` convention.
2. Add it to the appropriate role array(s) in `PermissionClaimsTransformer`.
3. Register the policy in `AuthorizationModule.cs` (one `.AddPermissionPolicy(Permissions.YourNew)` line).
4. Apply `[Authorize(Policy = AuthorizationModule.PolicyPrefix + Permissions.YourNew)]` to the relevant controller or action.

## Replacing hardcoded data with database values

The role-to-permission mapping is currently hardcoded in `PermissionClaimsTransformer`. It is marked with `// TODO` comments pointing to this section.

**Migration path:**

1. Add a `Permission` entity and a `RolePermission` join entity to `db/Models/UserManagement/`.
2. Create and run a migration to seed the initial data from the hardcoded mappings.
3. Create `IPermissionService` (e.g., in `Unified.UserManagement`) with a method:
   ```csharp
   Task<IReadOnlyList<string>> GetPermissionsForRolesAsync(IEnumerable<string> roles);
   ```
4. Inject `IPermissionService` into `PermissionClaimsTransformer` and replace the hardcoded mapping with the async DB call.

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
