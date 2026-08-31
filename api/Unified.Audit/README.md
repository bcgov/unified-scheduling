# Unified.Audit

Query API over the append-only `AuditRecord` table (`db/Models/AuditRecord.cs`): paginated/filterable
audit history, recorded entity types, and per-entity-type auditable field schema.

This module does not write `AuditRecord` rows itself — no `SaveChangesInterceptor` is currently
registered to capture entity changes. `AuditController` (`GET /api/audit/history`,
`GET /api/audit/schema/entity-types`, `GET /api/audit/schema/entity-types/{entityType}/fields`)
only reads rows that already exist in the table.

## Registration

`AddAuditModule()` registers `AuditRecordOptions` (bound from configuration — currently just the
`ExcludedPropertyNames` deny-list used by `AuditSchemaService` to decide which fields are
filterable/displayable), `ICurrentActorResolver` (`HttpContextActorResolver`, which resolves the
current user from the `ClaimsPrincipal`, falling back to the platform system user when there is no
authenticated user), `IAuditHistoryService`, `IAuditSchemaService`, and the `AuditRead` permission
policy.


