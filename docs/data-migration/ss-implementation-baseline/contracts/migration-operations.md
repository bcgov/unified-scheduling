# Migration Operations Contract

**Design baseline 2026-11-16.** Use existing Unified authorization with a migration-operations
permission. These are required operations, not claims that endpoints exist.

## Scope And Keys

Source is SS or deferred CASS. SS Job is MyTeam, Training, Scheduling or Report.
Every request validates source/job/run ownership. A source-wide control affects all its
jobs but never another source. Return stable run IDs, pagination and safe diagnostics.
Unknown/disabled/config-invalid values fail explicitly; running/lineage races return conflict.

| Operation | Preconditions / result |
|---|---|
| Status / runs / run detail | Authorized; source/job-specific config/control/counts/lineage/verdict and redacted failures |
| Trigger | Enabled config, JC/cutover guards, atomic same-lineage dependency gate; stable accepted run ID |
| Dry run | Same guards/mappers; actual DML rolled back, evidence/candidate staging only; DryRun defaults true |
| Pause / resume | Audited actor/time; job or whole source; resume never overrides config |
| Abort | Audited request; no further batch selection after current batch boundary |
| Stale-run recovery | Explicit failure/recovery evidence and fencing; no concurrent stale worker commits |
| Final Report | All loaders Completed from same MyTeam lineage, none Running; safe findings/CSV/Markdown |
| Rollback preview | No business writes; restricted export of later edits, dependants, owned targets and immutable impact digest |
| Rollback apply | `ConfirmDataLoss=<runId>` plus current impact digest; dependent-first, all-or-none per job |

## Dependency And Safety Rules

Training/Scheduling need latest Completed MyTeam with no parent Running. They may overlap
each other. MyTeam cannot overlap either or Report. Report cannot overlap loaders and must
read one explicit lineage/snapshot. Dry-run parents never authorize applied dependent writes.

Report verdict: Pass; PassWithKnownDifferences; NeedsAction; Fail. Unknown/failed/rejected
rows, unexplained coverage/differences or primary-goal failure never pass. Any open NeedsAction
prevents both passing verdicts.

Rollback refuses MyTeam while applied dependants remain. Merge/synthetic deletion requires
whole-lineage ownership. Link-only existing targets and unrelated new work remain.
Confirmed rollback may overwrite later migrated-target edits only after protected export.
Revalidate impact before apply; state changes invalidate stale confirmation.

## Data Protection

Ordinary responses/findings/logs expose only safe keys, classes, counts and metrics.
Never return connection strings, credentials, photos, protected source comments or raw errors.
Rollback impact values are intentionally sensitive: separate restricted storage/download,
audited access and retention; not ordinary findings or a public report.
No automatic backup restore, source writes or production enablement.
