# ADR: Standardize feature module endpoint registration

- Status: Accepted
- Date: 2026-08-27
- Deciders: @BronzBierd, with input from @hrandhawa13
- Technical area: API module composition and feature-gated endpoints
- Related: [Issue #119](https://github.com/bcgov/unified-scheduling/issues/119)

## Context

Feature-gated modules must not expose routes when disabled. Issue #119 records
three patterns: always-on MVC for UserManagement, module-owned minimal APIs for
Stats, and conditional MVC application parts for Calendar and Training. The
existing application-part approach makes disabled controller assemblies absent
from MVC discovery and avoids exposing their routes or API Explorer metadata,
but its registration logic is duplicated between modules.

## Decision

We will use `ApplicationPartManager` for controller-based, feature-gated
modules. Each module will conditionally register its controller assembly at
startup, so a disabled module's controllers are absent from MVC discovery and
do not contribute routes or OpenAPI entries. The repeated registration logic
will be consolidated into one shared generic
`ModuleApplicationPartExtensions.AddConditionalApplicationPart<TMarker>()`
helper.

This decision does not migrate controller endpoints to minimal APIs. Stats may
retain its existing minimal-API implementation, while always-on UserManagement
remains out of scope.

## Alternatives

- Minimal APIs with explicit `MapXxxEndpoints()`: not selected because migrating
  existing controller endpoints changes their implementation model without
  providing enough benefit over the already working application-part approach.
  The `MapXxxEndpoints()` was in fact inert as the endpoints were already mapped
  through normal controller discovery.
- Per-request routing policies or middleware: not selected because disabled
  endpoints would remain in endpoint metadata and would require separate
  handling to keep them out of OpenAPI.
- Duplicated application-part registration per module: not selected because a
  shared generic helper removes the repeated, fragile assembly-list logic.

## Consequences

- Benefit: Disabled controller modules are genuinely absent from MVC discovery,
  routing, and OpenAPI, with no endpoint migration required.
- Trade-off: Controller-based modules retain the MVC dependency; enablement is
  evaluated at startup and applies at assembly granularity.
- Mitigation: Use the shared generic helper and test both enabled and disabled
  module routing and OpenAPI behavior.

## Follow-up

- Add the shared `ModuleApplicationPartExtensions` helper and refactor Calendar
  and Training to use it.
- Apply this convention to future controller-based feature-gated modules and
  document any intentional minimal-API implementations such as Stats.
- Preserve the accepted assembly-level and startup-only constraints in module
  design and tests.

## References

- [Application startup and module wiring](../../api/Unified.Api/Program.cs)
- [Stats module endpoint mapping](../../api/Unified.Stats/StatsModule.cs)
- [Calendar application-part registration](../../api/Unified.Calendar/CalendarModule.cs)
- [Training application-part registration](../../api/Unified.Training/TrainingModule.cs)
- [Scheduling module](../../api/Unified.Scheduling/SchedulingModule.cs)