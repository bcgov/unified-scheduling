---
name: unified-permission
description: "Add or update permission seed entries using module-owned PermissionSeedConfiguration contributions, then let PermissionSeeder aggregate across modules. Use when introducing a new permission or moving permissions between modules."
argument-hint: "Add or update permissions, provide permissions enum name, and description"
---

# Module Permission Seeding

## Purpose
Use this workflow to add permissions without centralizing feature logic in one seeder file.
Each module contributes its own permission seed entries; the shared seeder aggregates all module contributions.

## Architecture
- Permission.cs: Enum of all permissions `api/Unified.Authorization/Permissions.cs`
- Shared contract: `api/Unified.Common/Seeders/PermissionSeedConfiguration.cs`
- Aggregator seeder: `api/Unified.UserManagement/Seeders/PermissionSeeder.cs`
- Module contributors (examples):
  - `api/Unified.Core/CoreModule.cs`
  - `api/Unified.UserManagement/UserManagementModule.cs`
  - `api/Unified.Stats/StatsModule.cs`

## Add A New Permission
1. Identify the owning module for the permission.
2. In that module's `*Module.cs`, add a `PermissionSeedDefinition` entry to its `PermissionSeeds` list.
3. Ensure policy registration exists in the same module via `.AddPermissionPolicy(Permissions.Xxx)` when authorization is required.
4. If the module is feature-flagged, keep feature-gating in `Program.cs` by conditionally calling `AddXxxModule()`.
5. Do not add feature-flag checks in `PermissionSeeder`.

## Example Pattern
```csharp
private static readonly PermissionSeedDefinition[] PermissionSeeds =
[
    new(nameof(Permissions.UsersView), "View users"),
];

services.AddSingleton(new PermissionSeedConfiguration("UserManagement", PermissionSeeds));
```

## Rules
- Keep permission ownership local to the module.
- Keep `PermissionSeeder` aggregation-only (no module-specific rules).
- Use stable IDs (`nameof(Permissions.Xxx)`) and clear descriptions.
- Avoid duplicate IDs across modules.

## Validation
Run targeted build after changes:
```bash
dotnet build api/Unified.Api/Unified.Api.csproj
```
