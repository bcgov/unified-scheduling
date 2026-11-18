# Validate SS Migration

**Updated 2026-11-16.** Design/run sequence; command results belong in
[validation evidence](./validation-evidence.md). Never run against production by default.

## Prerequisites

- Existing `Feature/SS-872-ss-migration-job`; no branch switch or pushes.
- Repository Dev Container/.NET 10 SDK container; host .NET 8 is not sufficient.
- Docker; isolated migration target and persistent synthetic SS source; no shared DB reset.
- Source schema from SS EF migrations and deterministic fake data shaped by SH aggregates.
- SS read-only connection supplied outside Git. Explicit false JC expiry flags.
- `HistoryStartDate=2020-01-01`, `CutoverDate=2026-12-01`; per-job disabled/dry-run defaults.
- Apply is disabled per job by default. An authorized operator must set that job's
  `AllowMigrationApply=true` and explicitly request JSON `dryRun:false` through the operations
  API. No second `DryRun=false` config is needed. Direct/recurring jobs cannot apply.
- Apply is not limited by target database name. Rollback remains restricted to its existing
  acknowledged synthetic database allowlist and is unavailable for production; plan
  recovery and backup/restore separately.

Use the committed implementation status/runbook index (local-only: `implementation-status.md`)
and [final adverse report](../../unified-scheduling/docs/data-migration/reports/2026-11-17-adverse-v20-final.md)
for exact commands and resource names. Shape mocks pass original SH SQL; the separate adverse
fixture deliberately does not. Current acceptance is **NO-GO**, not permission to enable jobs.

## Smallest Code Checks

From `unified-scheduling`, in its supported .NET 10 tool environment:

```powershell
rtk dotnet build api\Unified.DataMigration\Unified.DataMigration.csproj
rtk dotnet test --project api\Unified.Tests\Unified.Tests.csproj --filter FullyQualifiedName~DataMigration
rtk git diff --check
```

The broad migration filter includes HTTP tests and PostgreSQL-gated tests; run it only when
those operations are explicitly approved. The direct-service safety tests can be selected
with `--filter-class Unified.Tests.DataMigration.MigrationApplySafetyTests`. Then run its actual
PostgreSQL integration harness, formatter and EF migration script/apply validation.
Broader suite/build is required when the affected paths or failures demand it; record baseline
failures distinctly from newly introduced failures.

## Rehearsal Sequence

1. Verify synthetic source health/schema/marker, read-only credentials and isolated target.
   Record container/volume names without secrets. Keep source up after work.
2. Measure shape: per-location/year counts, time/duration/weekday and per-user distributions.
   Require +/-20% on comparable unsuppressed buckets; `<5` is not an exact count.
3. Generate actual SS oracle exports for fixed past/cutover/future windows.
4. Verify all disabled jobs have no active schedule; CASS has no reads/runs.
5. Run dry MyTeam -> dry Training/Scheduling -> Report. Business writes must roll back;
   Durable candidate parents now rehydrate into child rollback scopes; wrong/stale lineage
   must reject honestly. Simulation Report is not applied acceptance.
6. On a fresh **isolated** target, record backup/footprint then apply MyTeam, followed by
   Training and Scheduling (parallel gate checked), then final Report.
7. Compare actual Unified reports/read/publish paths to SS oracles through crosswalks.
   Test the unmodified adverse fixture first: expected Fail/NeedsAction is a valid test,
   not an accepted migration. Keep a separately named ready fixture when testing a pass.
8. Replay unchanged, change source, edit target, force batch failure, interrupt/resume and
   move cutoff earlier. Verify no duplicate/link loss/overwrites and durable retry.
9. Preview rollback with later edits/dependent new work. No writes without current-impact
   confirmation. Apply reverse-order rollback only on the isolated rehearsal target.
   Verify footprint restored and no migrated rows in report queries.
10. Record each SS-V ID with exact tests/results. Do not equate loader Completed with Pass.

## Acceptance And Manual Go-Live

Report must account for every selected source row; all unexplained differences fail.
Open NeedsAction never passes. Check identity/auth material, schedule business meaning and
training compliance fields explicitly. Synthetic login identifiers do not prove real IDIR login.
Report generation errors do not fail loader runs or block this apply effort. When run state
is writable, record `NeedsAction`, not a pass; surface persistence errors. Genuine
data-validation failures remain failures.

Operator go-live tasks: actual backup/restore check, explicit JC config, read-only secrets,
resolve future Surrey gap groups, final actual report comparison and publish preview/approval
per location/date. Future Draft shifts are not staff-visible until published.
CB6495 deployment/config and production writes need separate approval.

Never automatically restore/reset a shared DB, enable production cron, push, or fix ordinary
Unified workflows as part of this migration.
