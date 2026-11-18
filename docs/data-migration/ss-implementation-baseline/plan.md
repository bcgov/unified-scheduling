# Implementation Plan: SS Migration And Validity

**Branch**: `Feature/SS-872-ss-migration-job` | **Updated**: 2026-11-16 |
**Spec**: [spec.md](./spec.md) | **Contract**: [SS mapping](./contracts/ss-mapping-contracts.md)

## Summary And Scope

Deliver migration-only code in `unified-scheduling`, a persistent Docker synthetic SS DB,
and evidence that identities, schedules and training business data survive the move.
Use Unified's existing workflows; do not rebuild SS or alter Unified application behavior.
No pushes. Commit logical milestones without attribution. Keep the existing branch and
unrelated worktree changes. CASS and tenant GitOps implementation are deferred.

The current request authorizes implementation and engineering decisions consistent with
these goals. T016 now means reconciliation of recorded decisions, not another blanket
mapping-approval wait. Production writes, deployment and go-live still require human approval.
Only unresolved failures of identity/authentication data, scheduling or training may block
acceptance. Other gaps are explicit findings/backlog, not excuses to stop implementation.

### Measured Delivery Disposition

Migration jobs, writers, recovery/replay, dry-run lineage, authorized operations and redacted
exports are implemented. SDK10 builds and 108 focused tests pass. Exact original SH SQL
passes two independently built production-shaped mocks; the adverse source is intentionally
separate. See [validation evidence](./validation-evidence.md) for precise denominators/results.

Acceptance remains **NO-GO**: BL-15 is a measured primary scheduling-service gap; source
future-gap repair/publication and unapproved adverse/OT/report differences also remain.
Synthetic identity persistence does not prove real IDIR login. Broader release verification
is incomplete. Do not waive these gates or alter ordinary Unified behavior to force migration.
Unchecked tasks retain their verification/acceptance obligations, not an implied production pass.

## Technical Context

| Item | Choice |
|---|---|
| Language/runtime | C#, .NET 10; legacy oracle .NET 5 kept separate |
| Jobs | API-triggered Hangfire jobs; optional recurring dry runs on Vancouver schedule |
| Target | Existing EF Core/Npgsql PostgreSQL model plus migration-owned state only |
| Source | SS PostgreSQL 12 schema from legacy EF migrations; explicit read-only Npgsql DTOs |
| Tests | Existing xUnit plus real PostgreSQL integration and actual report/service comparisons |
| Local tools | Prefer repository Dev Containers; SDK containers when host SDK cannot build |
| Persistent resources | Dedicated Compose project/named volumes, isolated source/target, health checks |
| Safety | No production data/access; no shared local DB reset; no app or tenant changes |
| Scale | Aggregate SH-01..08 shape, ±20% required fit; planted rare/adverse cases |

## Constitution Check

- Privacy: invented staff/text, aggregate-only source evidence, no payload/secret logging.
- Simplicity: source-specific readers and mappers in existing module; no new generic platform.
- Ownership: Unified-uops-lead owns migration module/state/tests/Docker; legacy-ss owns
  SS oracle code if needed; architect owns workspace artifacts and synthesis.
- Quality: exact business-field, workflow, replay, failure and report checks; no fake passes.
- Accountability: keep real failures visible; manual production/cutover sign-off remains.
- Reproducibility: isolated container tooling/data; leave synthetic source up.
- Living docs: stable rule/test/backlog IDs and measured evidence, not copied investigation logs.

Scope override: older T051 JC code-default change is backlog only. Migration itself requires
all three expiry flags explicitly false and fails closed otherwise. No non-migration app fix.

## Implementation Gate: Mapping Contracts

Phase 0 is complete for build authorization; this gate no longer blocks implementation.
Production enablement still requires the validity evidence and human approval below.

Use the [final worksheet](../../documentation/data-migration/ss-job-notes/mapping-decision-worksheet.md),
[design](../../documentation/data-migration/migration-job-design.md), EX/LX/AG/AD/SP/OD and
the production [SH extract](../../documentation/data-migration/ss-job-notes/ss-shape-extracts.md).
Promote settled Q/G rules into the canonical contract, clear Q-T6C's stale client gate,
transfer BL-1..6 and every named app gap to [backlog](./backlog.md).
Engineering choices EN-1/2/4/5/6/7 are specified below, not new business-approval gates.

## Phase 1: Job And State Design

### Jobs And Dependencies

| Job | Stable Hangfire ID | Prerequisite |
|---|---|---|
| MyTeam | `data-migration:ss:myteam` | No Training/Scheduling/Report Running |
| Training | `data-migration:ss:training` | Latest MyTeam Completed; no MyTeam Running |
| Scheduling | `data-migration:ss:scheduling` | Same; Training need not be done |
| Report | `data-migration:ss:report` | All three Completed, same MyTeam lineage, none Running |

Training/Scheduling may run in parallel; enforce the gate and insert run in one transaction
under a source advisory transaction lock. Database uniqueness is `(Source, Job)` for Running.
Persist prerequisite MyTeam run ID, dry-run lineage, heartbeat/lease and recovery evidence.
An expired worker cannot resume writes after takeover: enforce a fencing token or equivalent
transactional lease ownership check. No automatic success for stale runs.

Config: `DataMigration:Sources:SS:Jobs:{MyTeam|Training|Scheduling|Report}` each has
Enabled, CronSchedule, DryRun=true, AllowMigrationApply=false, BatchSize. Disabled/empty cron removes schedule.
Apply requires an authenticated, authorized API trigger with explicit JSON `dryRun:false`
and the selected job's `AllowMigrationApply=true`; it does not require `DryRun=false`.
The worker rechecks that gate, the recorded trigger audit, run mode and configuration.
Direct and recurring execution cannot apply. Their configured `DryRun=false` is refused.
Apply has no target-name allowlist; rollback retains its separate rehearsal-only guard.
SS jobs share their source connection; Unified supplies the target connection. An independent
backup/restore procedure and explicit owner approval are recommended before real writes.
Validate entity ownership at startup. CASS stays disabled and source-isolated.
Controls are per `(Source, Job)` plus source-wide pause/abort. Poll at batch boundaries.

`CutoverDate=2026-12-01`; history starts `2020-01-01` Vancouver midnight.
Freeze cutover after applied work. One immutable UTC run start supplies insert-only fallbacks.
JC is scheduled apart by operators, outside this DB gate.

### Persistence And Transactions

See [data-model](./data-model.md). Add only migration state/schema. Shared live crosswalk
identity remains `(SS, entity, source key)`, independent of Job. Dry-run candidate mappings
must be in separate migration-owned staging, scoped to a dry-run lineage and never usable
by applied runs. Do not weaken live crosswalk uniqueness or persist rolled-back target IDs
as if those targets existed.

For Validate mode, the same mappers execute real target DML in rolled-back transactions.
Dependent simulation must rehydrate candidate parents in its rollback scope; a staged ID
alone cannot satisfy an FK. If full-chain validation cannot be implemented safely, reject
MissingParent explicitly and use a fully applied isolated rehearsal for acceptance.
Never silently substitute a separate no-DML validation path.

### Replay, Retry And Hashing (EN-1/4/5)

- Per physical table, page by `(COALESCE(UpdatedOn, CreatedOn), typed source ID)`.
  Numeric IDs sort numerically; GUIDs use consistent database ordering. Capture a run upper
  bound; replay overlap handles ties but is not a substitute for pending-row retry.
- Persist source-keyed rejected/blocked work; retry independently of advanced normal cursors.
  A failing batch never advances its cursor. Independent rows continue via subsequent
  bounded batches; terminal reconciliation must still fail unresolved primary-goal gaps.
- Persist applied cutoff and separate earlier-cutoff backfill cursor/range per table.
  Interrupted backfill resumes; overlapping changes are replay-safe; later cutoff deletes none.
- Canonical fingerprint: explicit mapped-field names in stable order, typed JSON null/booleans/
  integers, enums by name, UTC instants in invariant round-trip form, exact mapped text,
  resolved relationship IDs. Exclude source audits, ignored fields and photos.
- Store source fingerprint separately from target AfterHash where mapping is not one-to-one.
  Merge fingerprints include sorted full membership and mapped values. Preserve insert-only
  defaults, allocated order/version and published status on replay.
- Lock target tables against ordinary writes before append-order MAX/read through commit
  (EN-2); do not change app writers to acquire a migration-specific advisory lock.

### Load Order And Side Effects (EN-6)

MyTeam: Regions -> Locations/parents -> Type 5/7 -> Roles -> Users -> UserRoles ->
acting/away/leave. Training: categories/courses -> histories -> qualifying user profile.
Scheduling: rooms/classifications -> merged definitions -> merged shifts -> entries ->
staff links -> qualifying series. Training alone owns Users.TrainingProfileId.

Use tracked EF writes and existing audits/FK checks, without UserCreatedSignal,
mandatory auto-assignment, renewal expiry calculation, regeneration or mail sends.
Historical API-only conflicts insert/report only where approved; future publish blockers stay
NeedsAction. Future Draft schedules require operator preview/publish before use.

## Phase 2: Synthetic Source And Oracle

Inspect/preserve existing `ss-synth-pg` and its data volume before any change. Never delete
or reset the current application DB in this shared environment; use a new isolated target.
Preserve existing mock artifacts, import reproducible migration-specific assets into the app
repo, verify generated schema and planted case manifest rather than trust prior notes.

Use aggregate SH shape and deterministic invented records. Compare actual counts and
start-hour/duration/weekday/per-sheriff distributions to ±20%, by non-suppressed comparable
bucket. Treat `<5` as a range; do not invent exact counts. Report untuned buckets honestly.
Verify seed reproducibility, refusal on filled DB, all extractors and every planted case.
Keep an unmodified adverse fixture run (expected Fail/NeedsAction) and a separately named
approved-ready fixture run; never erase bad cases merely to obtain Pass.

Run actual SS report services against the synthetic source. Export goldens for O-1 training,
O-2 shifts, O-3 distribute schedule and O-4 roster. O-5 stored/rule overtime is deferred
with T063 to BL-17; it is not a production feature or release acceptance gate.
Use past month, cutover +/-14 days and first future month per active location.
Map identities through real crosswalks; compare instants and business values, not layout.

## Phase 3: Findings And Acceptance

Final Report writes `DataMigrationFindings` and restricted CSV/Markdown exports only.
Each selected source row has exactly one final outcome; group metrics are separate.
Known catalogue comes from the contract, with distinct synthetic and production thresholds.
Include declared exclusions, held future gap shifts, missing authentication material,
calendar/publish blockers, all changed overtime days, training fields/status differences.

Verdicts:
- Pass: only Migrated/Merged/approved Excluded.
- PassWithKnownDifferences: approved bounded differences, zero unresolved NeedsAction.
- NeedsAction: open human action; **not a pass**.
- Fail: coverage hole, unexpected/rejected/failed class, threshold breach or unmet primary goal.

Report generation errors are non-blocking for the apply effort: the Report run records
`NeedsAction` and safe diagnostics, while loader outcomes remain unchanged. A missing
oracle validator is not a pass. Actual data failures remain visible and are assessed
separately; the O-1..O-4 validator is not part of this apply change.

Do not treat a matching row count as workflow parity. Verify actual Unified read paths,
future edit/publish compatibility, compliance report business fields and authentication
identity resolution. No real IDIR login can be proved by a synthetic identity; report that limit.

## Phase 4: Recovery

Keep a restore-point backup ID and migration-footprint count/hash before applied work.
Reverse Report -> Scheduling/Training -> MyTeam; refuse parents with live dependent lineage.
Link-only targets never become delete candidates. Delete merged/synthetic targets only when
all contributing links belong to the rollback lineage.

Default rollback is preview only. Export later edits/dependent new work in a **restricted,
PII-bearing rollback impact artifact**, never ordinary findings/logs. Bind explicit
`ConfirmDataLoss=<runId>` to an immutable impact digest and revalidate it before apply.
Authorized confirmed rollback may overwrite later edits per the accepted design.
One transaction per job; failed rollback changes none. Preserve unrelated work, reset
appropriate cursors/cutover lock and verify migration-footprint hashes. Full backup restore
is a separate destructive operator step, never an automatic shortcut in this shared environment.

## Delivery Milestones And Completion

1. Reconciled spec/contract/tasks/backlog and repo-local implementation spec.
2. Persistent synthetic DB/harness, schema and measured shape evidence.
3. Job/state/readers and MyTeam; then Training and Scheduling.
4. Report, dry run, recovery and actual workflow/parity checks.
5. Evidence/gaps/training comparison and logical commits; no push.

Existing T001..T052 IDs stay traceable in [tasks](./tasks.md); T053+ track the new phases.
Do not mark unchecked validation IDs complete without a linked command/test and actual result.
Production enablement, CB6495 changes and non-migration app backlog stay out of this build.
No extension hooks file is installed; no before/after-plan hooks apply.
