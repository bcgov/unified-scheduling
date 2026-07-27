---
name: unified-seed-data
description: "Add or modify configurable EF Core seed data using central seed-data composition and deployment-selected atomic data sets. Use when changing seeders, seed definitions, or SeedData configuration."
argument-hint: "Describe the seed data set or seeder to add or change"
---

# Configurable Seed Data

## Purpose

Use this pattern when the unified backend needs the same table-specific executable seeder to receive different combinations of data. Modules register executable seeders; deployments select data sets through configuration.

## Architecture

- Seeder registration: `api/Unified.Common/Seeders/SeederBase.cs`
- Table-specific aggregator seeders which define seed logic but not data: `api/Unified.UserManagement/Seeders/*Seeder.cs`
- Seed data contracts: entity-specific contracts in the owning module's `Seeders/` folder; permission contracts in `api/Unified.Authorization/Seeders/`
- Atomic data sets and descriptors consumed by seeder(s): module-specific `*SeedData.cs` and `*SeedDataSets.cs`
- Central selection responsible for parsing appsettings configuration: `api/Unified.Api/Services/SeedDataComposition.cs`
- Deployment selection: `SeedData:DataSets` in environment-specific configuration

`SeederFactory` executes only seeders explicitly registered through `AddSeeder`; do not reintroduce reflection-based seeder discovery.

## Add a Data Set

1. Keep table-specific insertion and update logic in a shared aggregator seeder.
2. Add an atomic `*SeedData` file containing only typed definitions for one reusable data set.
3. Add a `nameof(...)`-based descriptor to the owning module's `*SeedDataSets` class with the appropriate `ISeedConfiguration` values.
4. Add the key to `SeedData:DataSets` only for deployments that require it. Use environment variables or environment-specific appsettings files to supply the complete list.
5. Add tests that verify the selected keys register the intended configurations and that invalid configuration is rejected.

## Configuration Example

```json
"SeedData": {
  "DataSets": [
    "PlatformSystemUserDataSet",
    "UserManagementPermissionsDataSet",
    "DefaultRolesDataSet",
    "SheriffRegionLocationDataSet"
  ]
}
```

## Rules

- Do not put client, application, or deployment identity checks in seeders or seed-data files.
- Do not duplicate table-specific upsert logic in data-set files.
- Register common data explicitly too; do not rely on an implicit "core" set.
- Keep required entries and feature-dependent availability checks on the owning module's descriptor.
- Preserve existing names and behavior unless a change is required for this architecture; avoid unrelated refactoring or cosmetic renames.
- Detect duplicate natural keys or IDs when aggregating data from multiple selected configurations.

## Permissions

`PermissionSeedDefinition` and `PermissionSeedConfiguration` belong to `Unified.Authorization`, the permission contract owner. Feature modules own their permission data and descriptors, which select those Authorization contracts through `SeedData:DataSets`; do not register configurations automatically from a module.

## Validation

Run the API test project:

```bash
dotnet test api/Unified.Tests/Unified.Tests.csproj
```
