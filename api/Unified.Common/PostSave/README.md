# Post-save handlers

`IPostSaveHandler` stages related changes **after the initial EF save succeeds,
but before commit**. Like `ISaveRule`, it is registered directly in DI and receives
the saving `DbContext` when invoked. Post-save handlers additionally declare an entity
type and action so the interceptor executes only matching handlers.

## Contract and registration

- `Type EntityType { get; }` identifies the entity type handled.
- `SaveAction Action { get; }` selects `Create`, `Update`, or `Delete`.
- `Task HandleAsync(DbContext db, SaveContext context, CancellationToken cancellationToken)`
  receives the context performing the save. Use `db.Set<T>()` to query and stage changes.
- `SaveContext` is a non-generic record containing `object Entity`, `SaveAction Action`,
  and `DateTimeOffset OccurredAtUtc`. Cast `Entity` to the declared type when needed.

Register implementations directly as scoped `IPostSaveHandler` services, for example
`services.AddScoped<IPostSaveHandler, AssignMandatoryTrainingOnUserCreationHandler>()`.
Modules own their handlers; Common has no domain-specific dependencies.

`PostSaveInterceptor` takes `IEnumerable<IPostSaveHandler>` and `TimeProvider`.
Handler instances are resolved upfront with the interceptor, so do not inject the
`DbContext` whose options are being constructed; use the supplied `db` instead.
Only handlers matching both entity type and action execute, sequentially in
registration order for each entity. Do not depend on ordering across entities.
No handlers is a valid no-op.

## Save flow

Services use ordinary `SaveChangesAsync`; no custom save wrapper is required.
Registered after `SaveRulesInterceptor`, the interceptor captures EF states
(`Added` → `Create`, `Modified` → `Update`, `Deleted` → `Delete`) and a UTC timestamp
in `SavingChangesAsync`, then dispatches in `SavedChangesAsync`.

For relational providers, `UnifiedDbContext` encloses the operation, including
audit writes, in one transaction:

1. Begin a transaction, or create an outer savepoint in the caller's transaction.
   Nested saves use distinct savepoint names.
2. Save the initial changes. Failure or cancellation here runs no handlers.
3. Await matching handlers with the saving context. Queries see persisted changes;
   created/updated entities are `Unchanged` until handlers stage more changes.
4. Save again only if changes remain. A per-context recursion guard prevents
   cascading dispatch: follow-up changes do not trigger more handlers and are not queued.
5. Commit the owned transaction, or release the savepoint without committing the caller's transaction.

Handler errors, cancellation, or follow-up save failures roll back the initial save
and handler writes. An outer savepoint preserves earlier unrelated work in a
caller-owned transaction; rollback uses `CancellationToken.None`.
Discard the failed scope/unit of work: database rollback does not restore EF's
accepted tracked state. Nonrelational providers such as InMemory cannot roll back.

Handlers may query and stage changes, but must not call `SaveChanges`, manage
transactions, send notifications, or perform external effects that rollback cannot undo.
This is not a post-commit notification mechanism. For `Delete`, the row is already
absent and the entity normally detached, but `SaveContext.Entity` still holds it.

Only asynchronous saves with `acceptAllChangesOnSuccess = true` are supported.
Synchronous saves, raw SQL, and bulk operations bypassing `SaveChangesAsync` do not dispatch.

## Seeding behavior

Post-save handlers run for ordinary `SaveChangesAsync` calls, including seeding
saves that go through EF. If seeding should avoid post-save side effects, use a
save path that does not trigger handlers or structure seed order so prerequisites
for handler behavior do not exist yet.