---
name: backend-service-logging
description: "Apply the unified-scheduling backend logging pattern for .NET API services. Use when adding or changing logging in api/ services, controllers, background startup services, exception handling, or tests that directly instantiate logged services. Covers ILogger constructor injection, Microsoft logging level guidance, structured message templates, sensitive data handling, and validation commands."
---

# Backend Service Logging

## Scope
Applies to backend .NET code under `api/`, especially service classes such as:

- `api/Unified.*/Services/*Service.cs`
- `api/Unified.Api/Services/*`
- tests that directly instantiate services with logger constructor parameters

Use this together with `unit-tests-api` when tests need to be added or updated.

## Constructor Pattern

Inject a typed logger through the primary constructor.

```csharp
using Microsoft.Extensions.Logging;

public sealed class StatRecordService(UnifiedDbContext db, ILogger<StatRecordService> logger)
    : IStatRecordService
{
}
```

For non-primary constructors, keep the same typed logger category:

```csharp
private readonly ILogger<GlobalExceptionHandler> _logger;
```

When tests instantiate a service directly, pass a null logger:

```csharp
using Microsoft.Extensions.Logging.Abstractions;

var service = new RoleService(db, accessor, validator, NullLogger<RoleService>.Instance);
```

## Logging Levels

Follow Microsoft logging-level guidance:

- `Trace`: Very high-volume, short-lived diagnostics only. Avoid in normal service code.
- `Debug`: Diagnostic details useful during development or troubleshooting. Use for read/query filters, branch decisions, skipped stale IDs, and not-found returns that are normal control flow.
- `Information`: Normal application flow and successful meaningful operations. Use for create, update, delete, assign, expire, save, migration, and startup milestones.
- `Warning`: Unexpected or degraded conditions that are handled and should be investigated, such as concurrency conflicts or authentication protocol problems.
- `Error`: Failures in the current operation. Prefer centralized exception handling for request exceptions instead of logging and rethrowing in services.
- `Critical`: Application-wide failure that can prevent startup or continued operation, such as migration failure during startup.

Do not log expected validation, forbidden, or not-found service paths as `Error`. Let `GlobalExceptionHandler` own exception logging for request failures.

## Message Style

Use structured logging templates, not string interpolation.

```csharp
logger.LogInformation("Updated stat record {StatRecordId}", id);
logger.LogDebug("Retrieving stat records for location {LocationId}, status {Status}", locationId, status);
```

Keep placeholders stable and specific:

- Prefer `{UserId}`, `{RoleId}`, `{LocationId}`, `{StatRecordId}`, `{RecordCount}`.
- Avoid generic placeholders such as `{Id}` or `{Value}` when a domain name is available.
- Include counts for batch operations.
- Include caller/user IDs when they materially explain an authorization-sensitive mutation.

## Sensitive Data

Do not log raw user-entered free text, names, comments, notes, emails, or search strings unless there is a clear operational need.

Use `Unified.Common.Logging.LogSanitizer` when a search or text field must be represented:

```csharp
logger.LogDebug(
    "Retrieving users with search provided {HasSearch}, search length {SearchLength}",
    LogSanitizer.HasValue(queryParams?.Search),
    LogSanitizer.Length(queryParams?.Search)
);
```

If the sanitized value itself is needed, use `LogSanitizer.UserText(...)` and keep the log at `Debug` unless there is a strong reason otherwise.

## Service Method Patterns

Read/query methods:

```csharp
logger.LogDebug("Retrieving stat category {StatCategoryId}", id);
```

Create/update/delete methods:

```csharp
logger.LogInformation("Creating role {RoleName} with {PermissionCount} permissions", request.Name, request.PermissionIds.Count);
logger.LogInformation("Created role {RoleId}", role.Id);
```

Normal not-found return:

```csharp
if (entity is null)
{
    logger.LogDebug("Stat record {StatRecordId} was not found for update", id);
    return null;
}
```

Branch details inside a mutation:

```csharp
logger.LogDebug(
    "Role {RoleId} permission changes: adding {AddedCount}, removing {RemovedCount}",
    role.Id,
    addedCount,
    removedCount
);
```

## Avoid

- Do not use string interpolation in log messages.
- Do not add broad `try/catch` blocks just to log and rethrow.
- Do not log the same exception in both a service and the global exception handler.
- Do not use `Information` for high-volume read paths unless the query is an important business event.
- Do not log secrets, tokens, raw comments, raw notes, raw search text, or `.env` values.

## Validation

After changing API logging or constructor signatures, run:

```bash
dotnet build api/Unified.Api/Unified.Api.csproj
```

If tests were touched, follow `unit-tests-api` and run:

```bash
dotnet test api/Unified.Tests/Unified.Tests.csproj
```
