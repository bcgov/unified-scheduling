---
name: save-rule-pattern
description: "Implement or modify ISaveRule business-rule validators that run inside EF Core's SaveChangesAsync pipeline for the unified-scheduling API. Use when adding a new ISaveRule, registering it via AddScoped<ISaveRule, ...>, or changing an existing rule under api/*/Rules/. Covers validation vs. database-constraint layering, transaction/rollback guarantees, EntityState filtering conventions, nested SaveChangesAsync re-entrancy (e.g. the AuditRecord insert Audit.NET performs after a successful save), and required test coverage."
---

# ISaveRule Pattern — Add Business Rule Validation

Use this skill when implementing business rule validation that must run before database commits.

## Quick Summary

**ISaveRule** is a pattern for validating business logic at the database layer:

```csharp
// 1. Create rule
public class EmailUniqueRule : ISaveRule
{
    public async Task ExecuteAsync(DbContext context, CancellationToken ct)
    {
        // Execute business logic
        // Throw on error → automatic rollback
    }
}

// 2. Register in module
services.AddScoped<ISaveRule, EmailUniqueRule>();

// 3. Done! Auto-discovered, runs before every SaveChanges()
```

## When to Use ISaveRule

Use ISaveRule when you need to:
- ✅ Validate business logic that requires database queries
- ✅ Protect against direct API writes (background jobs, seeders)
- ✅ Enforce constraints that validators can't catch (uniqueness across transactions)
- ✅ Audit changes before they commit
- ✅ Prevent data corruption from any code path

❌ **Don't use for:**
- Input validation (use FluentValidation instead)
- Simple field checks (database constraints work fine)
- Non-critical warnings (rules must throw to indicate errors)

## Step-by-Step Implementation

### Step 1: Create Rule Class

**File**: `api/Unified.YourModule/Rules/YourBusinessRuleRule.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Unified.Common.Interceptors;
using Unified.Db;
using Unified.Db.Models;

namespace Unified.YourModule.Rules;

/// <summary>
/// Describe what this rule validates and when it runs.
/// 
/// Example: Ensures new items have unique codes before SaveChanges.
/// Runs inside transaction - any exception causes rollback.
/// </summary>
public sealed class YourBusinessRuleRule(
    IOptionsMonitor<YourModuleFeatureFlags> featureFlagsMonitor
) : ISaveRule
{
    public async Task ExecuteAsync(DbContext context, CancellationToken cancellationToken)
    {
        // 1. Skip if feature flag disabled (optional)
        if (!featureFlagsMonitor.CurrentValue.YourFeature.Enabled)
            return;

        // 2. Get entities being created/modified
        var entries = context.ChangeTracker.Entries<YourEntity>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
            .ToList();

        if (!entries.Any())
            return;

        // 3. Query with the same DbContext (safe as long as rule does not call SaveChanges)
        var existingCodes = await context.Set<YourEntity>()
            .AsNoTracking()
            .Select(e => e.Code)
            .ToListAsync(cancellationToken);

        // 4. Validate and throw on error (triggers rollback)
        var duplicateCodes = entries
            .Select(e => e.Entity.Code)
            .Where(code => existingCodes.Contains(code))
            .ToList();

        if (duplicateCodes.Any())
        {
            throw new InvalidOperationException(
                $"Code(s) {string.Join(", ", duplicateCodes)} already exist."
            );
        }
    }
}
```

### Step 2: Register in Module

**File**: `api/Unified.YourModule/YourModuleModule.cs`

```csharp
using Unified.Common.Interceptors;
using Unified.YourModule.Rules;

namespace Unified.YourModule;

public static class YourModuleModule
{
    public static IServiceCollection AddYourModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ... existing registrations ...

        // Register save rules for business logic validation
        services.AddScoped<ISaveRule, YourBusinessRuleRule>();

        return services;
    }
}
```

### Step 3: Add Feature Flag (Optional)

If your rule needs conditional logic:

**File**: `api/Unified.YourModule/FeatureFlags/YourModuleFeatureFlags.cs`

```csharp
public class YourModuleFeatureFlags : IFeatureFlags
{
    public bool Enabled { get; set; }
    
    public YourFeatureFlags YourFeature { get; set; } = new();
}

public class YourFeatureFlags
{
    public bool Enabled { get; set; }
    public bool Required { get; set; }
}
```

**File**: `appsettings.json`

```json
{
  "FeatureFlags": {
    "YourModule": {
      "Enabled": true,
      "YourFeature": {
        "Enabled": true,
        "Required": true
      }
    }
  }
}
```


### Step 4: Write Tests

**File**: `api/Unified.Tests/YourModule/Rules/YourBusinessRuleRuleTests.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Xunit;
using Unified.Common.Interceptors;
using Unified.Db;
using Unified.Db.Models;
using Unified.YourModule.Rules;
using Microsoft.Extensions.Options;

namespace Unified.Tests.YourModule.Rules;

public class YourBusinessRuleRuleTests : IAsyncLifetime
{
    private UnifiedDbContext _dbContext = null!;

    public ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new UnifiedDbContext(options);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    private YourBusinessRuleRule CreateRule()
    {
        var featureFlags = new YourModuleFeatureFlags { Enabled = true };
        var monitor = new FakeOptionsMonitor<YourModuleFeatureFlags>(featureFlags);
        return new YourBusinessRuleRule(monitor);
    }

    [Fact]
    public async Task ExecuteAsync_ValidEntity_Passes()
    {
        // Arrange
        var rule = CreateRule();
        var entity = new YourEntity { Code = "UNIQUE_CODE" };
        _dbContext.YourEntities.Add(entity);

        // Act & Assert - should not throw
        await rule.ExecuteAsync(_dbContext, CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateCode_Throws()
    {
        // Arrange
        var rule = CreateRule();
        
        // Add existing entity
        _dbContext.YourEntities.Add(new YourEntity { Code = "DUP_CODE" });
        await _dbContext.SaveChangesAsync();

        // Try to add duplicate
        var newEntity = new YourEntity { Code = "DUP_CODE" };
        _dbContext.YourEntities.Add(newEntity);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => rule.ExecuteAsync(_dbContext, CancellationToken.None)
        );
        Assert.Contains("already exist", ex.Message);
    }

    private class FakeOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
```

## Common Patterns

### Pattern 1: Validate on Creation Only

```csharp
var newEntities = context.ChangeTracker.Entries<YourEntity>()
    .Where(e => e.State == EntityState.Added)
    .ToList();
```

### Pattern 2: Validate Changes to Specific Fields

```csharp
var modifiedEntities = context.ChangeTracker.Entries<YourEntity>()
    .Where(e => e.State == EntityState.Modified && e.Properties
        .Any(p => p.IsModified && (p.Metadata.Name == "Email" || p.Metadata.Name == "Code"))
    )
    .ToList();
```

### Pattern 3: Get Original vs. Current Values

```csharp
foreach (var entry in entries)
{
    var originalEmail = entry.OriginalValues["Email"].ToString();
    var currentEmail = entry.CurrentValues["Email"].ToString();
    
    if (originalEmail != currentEmail)
    {
        // Email was changed
    }
}
```

### Pattern 4: Multiple Entity Types

```csharp
var users = context.ChangeTracker.Entries<User>().ToList();
var roles = context.ChangeTracker.Entries<Role>().ToList();

// Validate both
await ValidateUsersAsync(users);
await ValidateRolesAsync(roles);
```

## Architecture

### Validation Layers

Rules operate in **three defensive layers**:

| Layer | When | Role |
|-------|------|------|
| **1. Validator (API)** | Input validation | Fast feedback, best UX |
| **2. ISaveRule (Any SaveChanges)** | Business logic | Protects all code paths |
| **3. DB Constraint** | Hard safety | Prevents data corruption |

```
API Endpoint
    ↓
FluentValidator (input check)
    ↓ (passes)
SaveChangesAsync()
    ↓
SaveRulesInterceptor
    ↓
foreach ISaveRule.ExecuteAsync()
    ↓ (all pass)
Database SaveChanges()
    ↓
DB Constraint (final check)
    ↓
✅ Commit or ❌ Rollback
```

### Transaction Guarantees

- ✅ All rules run **inside** the SaveChanges transaction
- ✅ Any exception **immediately triggers rollback** (data never committed)
- ✅ Original exception message propagates to caller
- ✅ Rules run sequentially, all have access to same DbContext state
- ✅ Querying with the same DbContext is safe when rule logic is sequential and does not call SaveChanges

### Nested SaveChangesAsync re-entrancy (audit record insert)

`UnifiedDbContext` inherits Audit.NET's `AuditDbContext` (see `api/Unified.Audit/README.md`). After a successful
save, Audit.NET writes the generated `AuditRecord` row via a **second, nested** `context.SaveChangesAsync()`
call on the *same* `DbContext` instance (bypassing the audit wrapper itself via `IAuditBypass`, but still going
through any EF Core `IInterceptor`s registered on the context, including `SaveRulesInterceptor`).

`SaveRulesInterceptor` explicitly guards against this: it skips the entire rule loop when the only pending
change is an `AuditRecord` entity, so rules never even run on that nested call - not just "happen to no-op".
This guard is a cheap, entity-type check, not a scoped suppressor flag.

**When adding or changing a rule, you still need `EntityState.Added || EntityState.Modified` filtering** (see
Common Mistakes below) for the general case of re-entrancy from *other* nested saves (e.g. a rule or service
that calls `SaveChangesAsync` more than once per request) - the `AuditRecord` guard only covers the audit
insert specifically.

If a rule has a side effect that must run exactly once per logical save (not once per physical
`SaveChangesAsync` call) - e.g. sending a notification, incrementing a counter, calling an external service -
prefer an explicit idempotency check in the rule itself over a suppression mechanism.

## Implementation Checklist

- [ ] Create rule class in `YourModule/Rules/`
- [ ] Implement `ISaveRule.ExecuteAsync()`
- [ ] Query with `context.Set<TEntity>().AsNoTracking()` for read checks
- [ ] Do not call `SaveChanges` or `SaveChangesAsync` inside rule logic
- [ ] Throw `InvalidOperationException` with clear message on error
- [ ] Register in `YourModuleModule.AddScoped<ISaveRule, YourRule>()`
- [ ] Add feature flag (optional but recommended)
- [ ] Add database constraint (optional but recommended)
- [ ] Add migration if DB constraint added
- [ ] Write unit tests covering:
  - ✅ Valid case (passes)
  - ✅ Invalid case (throws with message)
  - ✅ Feature flag disabled (skips)
- [ ] Add integration test showing SaveChanges rollback
- [ ] Update module README documenting new rule
- [ ] Run tests: `dotnet test api/Unified.Tests/`
- [ ] Verify locally with `dotnet build`

## Agent Validation Rules (Required)

Before finalizing any SaveRule change, the agent must validate all of the following:

1. Lifetime safety:
    - `SaveRulesInterceptor` is registered as scoped.
    - `ISaveRule` implementations are registered as scoped.
    - No singleton dependency chain is introduced from rule/interceptor.

2. Rule safety:
    - Rule does not call `SaveChanges`/`SaveChangesAsync`.
    - Rule queries are read-only and use `AsNoTracking()` where appropriate.
    - Rule can handle both `Added` and `Modified` entities when business logic requires both.

3. Duplicate-check quality:
    - Rule detects duplicates in pending changes (`ChangeTracker`) before database checks.
    - Rule detects duplicates in existing persisted records.
    - Rule aggregates all duplicates into one clear message (no first-hit-only throw).

4. Test coverage (minimum):
    - Valid case passes.
    - Missing required field throws.
    - Duplicate in pending changes throws.
    - Duplicate in database throws.
    - Multiple duplicates are aggregated in message.

5. Verification commands:
    - `dotnet build api/Unified.Api/Unified.Api.csproj`
    - `dotnet test --project api/Unified.Tests/Unified.Tests.csproj -- --filter-class Unified.Tests.UserManagement.Rules.UserBadgeNumberUniqueRuleTests`

6. Nested-save re-entrancy (required check):
    - Confirm the new/changed rule still filters by `EntityState.Added`/`Modified` before doing any work (see
      "Nested SaveChangesAsync re-entrancy" above). If it doesn't, or has non-idempotent side effects, add an
      explicit idempotency check before merging.

## Common Mistakes

| Mistake | Fix |
|---------|-----|
| Calling `SaveChanges` from inside a rule | Do not call `SaveChanges`; run read-only queries and throw on violations |
| Rule doesn't throw, just logs | Throw exception so transaction rolls back |
| Register as wrong interface | Use `services.AddScoped<ISaveRule, YourRule>()` |
| Include sensitive data in exception message | Sanitize before throwing |
| Test only calls ExecuteAsync without SaveChanges | Test full flow: Add entity → SaveChangesAsync should trigger rule |
| Rule only checks new entities, misses updates | Filter by `EntityState.Added \|\| EntityState.Modified` |
| Rule has side effects or reacts to `Unchanged` entries | Rule could double-run on any nested `SaveChangesAsync` call - see "Nested SaveChangesAsync re-entrancy" above |

## Related Files

- **Core Infrastructure**: `api/Unified.Common/Interceptors/`
  - `ISaveRule.cs` — Interface definition
  - `SaveRulesInterceptor.cs` — Runs all rules

- **Examples**:
  - `api/Unified.UserManagement/Rules/UserBadgeNumberUniqueRule.cs` — Real-world implementation example

- **Nested-save source** (see "Nested SaveChangesAsync re-entrancy" above):
  - `db/UnifiedDbContext.cs` — Inherits Audit.NET's `AuditDbContext`; the audit record insert is the source of
    the nested `SaveChangesAsync()` call
