---
name: unified-permission
description: "Add or update permission seed entries using module-owned data files selected by central seed-data composition. Use when introducing a new permission or moving permissions between modules."
argument-hint: "Add or update permissions, provide permissions enum name, and description"
---

# Module Permission Seeding

## Purpose
Use this workflow to add permissions without centralizing feature logic in one seeder file.
Each module owns its permission data, while central seed-data composition selects which data sets contribute to the shared seeder.

## Architecture
- Permission.cs: Enum of all permissions `api/Unified.Authorization/Permissions.cs`
- Shared contract: `api/Unified.Common/Seeders/PermissionSeedConfiguration.cs`
- Aggregator seeder: `api/Unified.UserManagement/Seeders/PermissionSeeder.cs`
- Module-owned data files (examples):
  - `api/Unified.UserManagement/UserManagementPermissionSeedData.cs`
  - `api/Unified.Stats/StatsPermissionSeedData.cs`
- Central configuration: `api/Unified.Api/Services/SeedDataComposition.cs`

## Add A New Permission
1. Identify the owning module for the permission.
2. In that module's `*PermissionSeedData.cs`, add a `PermissionSeedDefinition` entry to its definitions list.
3. Ensure the data set has a catalog entry in `SeedDataComposition` and is selected through `SeedData:DataSets` where needed.
4. Ensure policy registration exists in the same module via `.AddPermissionPolicy(Permissions.Xxx)` when authorization is required.
5. If the module is feature-flagged, keep feature-gating in `Program.cs` by conditionally calling `AddXxxModule()`.
6. Do not add feature-flag checks in `PermissionSeeder`.

## Example Pattern
```csharp
public static IReadOnlyList<PermissionSeedDefinition> Definitions { get; } =
[
    new(nameof(Permissions.UsersView), "View users"),
];
```

## Rules
- Keep permission data ownership local to the module.
- Register permission configurations only through `SeedDataComposition`, never from a module's service registration.
- Keep `PermissionSeeder` aggregation-only (no module-specific rules).
- Use stable IDs (`nameof(Permissions.Xxx)`) and clear descriptions.
- Avoid duplicate IDs across modules.

## Validation
Run targeted build after changes:
```bash
dotnet build api/Unified.Api/Unified.Api.csproj
```
