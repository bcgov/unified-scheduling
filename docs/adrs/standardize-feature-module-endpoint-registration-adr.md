# ADR: Standardize feature module endpoint registration

- Status: Proposed
- Date: 2026-08-18
- Deciders: Not identified; issue #119 was discussed by @BronzBierd and @hrandhawa13
- Technical area: API module composition and feature-gated endpoints
- Related: [Issue #119](https://github.com/bcgov/unified-scheduling/issues/119)

## Context

Feature-gated modules must not expose routes when disabled. Issue #119 records
three patterns: always-on MVC for UserManagement, module-owned minimal APIs for
Stats, and conditional MVC application parts for Calendar and other modules.
The discussion favors the Stats pattern, but migration scope and MVC exceptions
are unresolved.

## Decision

New feature-gated modules should own explicit `MapXxxEndpoints()` methods, and
both service registration and endpoint mapping must respect module enablement.
Calendar, Scheduling, and Training are candidate migrations; always-on
UserManagement is out of scope.

## Alternatives

- Conditional MVC application parts: preserves controllers, but makes endpoint
  discovery indirect and feature gating framework-specific.
- A mixed per-module approach: avoids migration, but keeps multiple conventions
  and weakens the module boundary.

## Consequences

- Benefit: New modules have one explicit, testable endpoint-registration
  boundary and disabled modules can omit their routes.
- Trade-off: Existing controller endpoints need migration and minimal-API
  conventions must cover metadata, authorization, and filters.
- Mitigation: Preserve route contracts and test disabled-module routing and
  OpenAPI output during each migration.

## Follow-up

- Set migration order and ownership for Calendar, Scheduling, and Training.
- Define minimal-API conventions and permitted MVC exceptions.
- Decide where the final feature-flag guard belongs.

## References

- [Application startup and module wiring](../../api/Unified.Api/Program.cs)
- [Stats module endpoint mapping](../../api/Unified.Stats/StatsModule.cs)
- [Calendar application-part registration](../../api/Unified.Calendar/CalendarModule.cs)
- [Training application-part registration](../../api/Unified.Training/TrainingModule.cs)
- [Scheduling module](../../api/Unified.Scheduling/SchedulingModule.cs)