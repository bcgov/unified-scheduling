# UserManagement Module

## Overview

The UserManagement module handles user CRUD operations, roles, permissions, acting positions, and away locations.

## Architecture

### Business Rules (ISaveRule Pattern)

The module uses the **ISaveRule pattern** for business rule validation that runs before database commits. Rules are registered in the module and auto-discovered by the `SaveRulesInterceptor`.

#### UserBadgeNumberUniqueRule

**Location**: `Rules/UserBadgeNumberUniqueRule.cs`

**Purpose**: 
- Validates badge number is required when `UserBadgeNumber.Required` feature flag is enabled
- Ensures badge numbers are unique in the database when `UserBadgeNumber.Enabled` feature flag is enabled

**Behavior**:
- Checks all users being created or modified
- Enforces required constraint: throws if `BadgeNumber` is null/empty when feature flag requires it
- Enforces uniqueness: throws if duplicate `BadgeNumber` exists for new or modified users
- Skips all validation if `UserBadgeNumber.Enabled` is false (feature flag disabled)

**Why not just validators?**
- Validators check API input only
- ISaveRule protects direct API writes, background jobs, and seeders
- Database constraint provides final safety net

## Feature Flags

### UserBadgeNumber

Controls badge number requirement and uniqueness across all deployments:

```json
{
  "FeatureFlags": {
    "UserManagement": {
      "Enabled": true,
      "UserBadgeNumber": {
        "Enabled": true,
        "Required": true
      }
    }
  }
}
```

**Parameters**:
- `Enabled`: When false, skips all badge number validation (required + uniqueness)
- `Required`: When true, badge number is mandatory for all users

## Validation Layers

### 1. **Validator** (API Input)
- `UserRequestValidator.cs` — Checks required based on feature flag
- Runs first, provides best UX with inline error messages
- Scope: API endpoints only

### 2. **ISaveRule** (Any SaveChanges)
- `UserBadgeNumberUniqueRule.cs` — Checks required + uniqueness based on feature flag
- Runs on every database save, regardless of entry point
- Scope: All code paths (endpoints, jobs, seeders, direct service calls)

### 3. **Database Constraint** (Hard Safety)
- `UserConfiguration.cs` — Unique index on `BadgeNumber` column
- Runs at database level, prevents corruption
- Scope: All writes, including direct SQL

## Testing

Run tests for the rule:

```bash
dotnet test api/Unified.Tests/Unified.Tests.csproj -- --filter-class Unified.Tests.UserManagement.Rules.UserBadgeNumberUniqueRuleTests
```

**Test Coverage**:
- ✅ Feature flag disabled — skips validation
- ✅ Unique badge number — passes
- ✅ Missing badge number when required — throws
- ✅ Modified user with missing badge — throws
- ✅ Duplicate badge in database — throws
- ✅ Duplicate badge on modify — throws

## Key Files

| File | Purpose |
|------|---------|
| `UserManagementModule.cs` | DI setup, registers rules and validators |
| `Validators/UserRequestValidator.cs` | API input validation with feature flag checks |
| `Rules/UserBadgeNumberUniqueRule.cs` | Business rule: required + uniqueness enforcement |
| `Models/UserRequestDto.cs` | Request DTO (BadgeNumber is optional, required by feature flag) |
| `FeatureFlags/UserManagementFeatureFlags.cs` | Feature flag configuration |

## Related Documentation

- [Database Interception Guide](../../docs/confluence/DATABASE-INTERCEPTION-GUIDE.md) — ISaveRule pattern details
- [Module Implementation Template](../../docs/confluence/MODULE-IMPLEMENTATION-TEMPLATE.md) — How to add new rules
