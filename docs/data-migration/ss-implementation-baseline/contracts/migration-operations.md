# Migration Operations Contract

**Design baseline 2026-11-16.** Use existing Unified authorization with a migration-operations
permission. These are required operations, not claims that endpoints exist.

Enabling `FeatureFlags:DataMigration:Enabled` requires explicitly selecting
`DataMigrationPermissionsDataSet` in deployment `SeedData:DataSets`, like the other
feature-owned permission datasets. It seeds only `DataMigrationOperate` and
`DataMigrationRollback` definitions: no roles, accounts or role grants. Selecting
it while the feature is disabled is rejected. Module registration alone does not seed
permissions; deployment owners must select the dataset and separately approve any grants.

## Scope And Keys

Source is SS or deferred CASS. SS Job is MyTeam, Training, Scheduling or Report.
Every request validates source/job/run ownership. A source-wide control affects all its
jobs but never another source. Return stable run IDs, pagination and safe diagnostics.
Unknown/disabled/config-invalid values fail explicitly; running/lineage races return conflict.

| Operation | Preconditions / result |
|---|---|
| Status / runs / run detail | Authorized; source/job-specific config/control/counts/lineage/verdict and redacted failures |
| Trigger | Authorized, audited API request; enabled config, JC/cutover guards and atomic same-lineage dependency gate; stable accepted run ID |
| Dry run | Same guards/mappers; actual DML rolled back, evidence/candidate staging only; DryRun defaults true |
| Apply | Authenticated operations API, resolved audited actor, explicit JSON `dryRun:false` and per-job `AllowMigrationApply=true`; no second `DryRun=false` setting |
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
Report generation or dispatch errors leave Report `NeedsAction`, with safe diagnostic evidence
and no increment to the failed count. Actual report/loader validation failures remain failures.
Loader technical-failure behavior is unchanged. An incomplete report never passes.
Missing independent oracle validation remains unverified; it is not part of this apply change.

Workers recheck the selected apply flag, immutable run settings and the matching trigger audit.
Direct/recurring jobs cannot apply; dry runs remain available. Target database names are not
an apply gate. The existing synthetic acknowledgement and database-name check remain for
rollback only: enabling apply does not enable rollback outside that isolated scope.
Production migration rollback remains unavailable.

Rollback refuses MyTeam while applied dependants remain. Merge/synthetic deletion requires
whole-lineage ownership. Link-only existing targets and unrelated new work remain.
Confirmed rollback may overwrite later migrated-target edits only after protected export.
Revalidate impact before apply; state changes invalidate stale confirmation.

Training and Scheduling may opt in to `PersistenceMode=Bulk` (bound into the trigger audit, set-based
chunks, no rollback journal, crosswalk-scoped `SsBulkUndo`, target role needs `TEMP`). Scheduling Bulk
refuses (`BulkRequiresPerRow:*`) any path it cannot reproduce exactly, including updates of already
migrated rows; see [bulk persistence](../training-bulk-persistence.md).

Scheduling PerRow may opt in to `PlanReplacementFamiliesOnly=true` (default `false`). The full graph
proposal plan is read only when a captured component family needs graph replacement. With the switch on, a
run that has no such family (first apply, unchanged rerun, recapture) skips planning and logs
`SsPhase ... Phase=Plan State=Skipped`. Any replacement family still plans everything, because the planners
read across components (day groups, definition groups). Outcomes, crosswalks and receipts are unchanged.

Scheduling PerRow may also opt in to `PrefetchReplayRecords=true` (default `false`). Existing crosswalk
records are read untracked, 1000 source-list rows at a time, instead of one query per row. A page
answers only the no-write verdicts (no crosswalk yet; unchanged hash and source timestamp) and only for a
record EF has not tracked in this job; any write, rebind, duplicate or rolled-back savepoint uses the per-row
query. Each source page is loaded at most once per job; after eviction, an interleaved access falls back to
the original per-row or group query rather than repeatedly loading the whole page. At most 16 pages per
type are cached. Graph replacement then tracks only the records of the replaced graph instead of every root record.
The run logs `SsReplayPrefetch Run=... Hits=... Fallbacks=... Pages=...`. Bulk ignores the switch.

## Run Audit Summary

For SS MyTeam, Training, Scheduling and Report, including simulation, the migration worker disables
Audit.NET **only on its own DbContext**, restores the previous flag on exit, and writes no general
`AuditRecord` per migrated business or bookkeeping row. The Bulk COPY audit path respects that same
context flag. The acquisition/recovery bookkeeping uses a similarly bounded scope and refuses a context
with pending changes. No process-wide audit switch or global entity exclusion is changed; concurrent
application contexts retain ordinary CRUD audit, EF stamp/save-rule/concurrency interceptors still run,
and Bulk retains its existing stamp and save-rule behavior.

The older source-agnostic `TryStartRunAsync` and `DataMigrationOrchestrator.ExecuteBatchAsync` helpers have
no callers in the current SS jobs. They are not an SS worker scope: a future SS caller using them for
business writes could still produce per-row audit records and must be adapted before use.

This deliberately loses granular general audit. It does **not** remove or replace per-record outcomes,
crosswalks, mutations, checkpoints, retry work items, controls or the existing PerRow rollback journals.
Bulk remains without a journal as before. Explicit `DataMigrationOperation` records for trigger,
pause/resume/abort, recovery, rollback preview/confirmation/impact download and findings export remain
separate from the summary. They are not suppressed; the existing apply trigger-evidence check remains.
Rollback is a separate operator action/receipt, not a second summary or a revision of the original summary.

Each terminal migration run gets one `AuditRecord` with `EntityType=DataMigrationRunSummary`,
`EntityPK=<run UUID>`, `TableName=DataMigrationRuns` and `Action=Added`. It contains the initiator ID
(operator at acquisition, or the system user for a scheduled run), source, job, persistence mode at
acquisition, dry-run flag, terminal status, safe completion cause where known, the persisted run counters,
start/completion/heartbeat timestamps and the cutover/history dates. A `Completed` loader is not business
acceptance. Older runs without acquisition metadata use matching trigger evidence where available;
otherwise their initiator/mode are reported as unknown, not invented.

The terminal status, its counters, final-batch evidence and summary commit together under the run-row fence;
earlier committed batches remain durable. A filtered unique index prevents a second summary for that UUID.
A summary insertion failure rolls back the terminal transition and is surfaced. If the process crashes before any terminal transaction commits, there is no
summary yet: the run remains `Running` with only the previously committed progress/trigger evidence.
Authorized audit-history readers can query `EntityType=DataMigrationRunSummary` and the run UUID; its field
schema is empty because summaries are `Added` records without changed-column filters.
An authorized stale-run recovery fences the worker, records `Abandoned` plus one summary atomically, and
keeps its existing recovery-action audits. No automatic summary is backfilled for already-terminal historical
runs. A retry of the same running UUID that observes different source snapshots marks the source projection
fingerprint unavailable rather than claiming either one describes the whole run.
If cancellation prevents a queued worker from acquiring the run, the existing recovery path is required
to terminalize it and create the summary; this change does not alter cancellation behavior.

### Fingerprints, version 1

These are SHA-256 digests, in uppercase hexadecimal, of the specified source projection captured before
business writes and evidence read during the terminal transaction. They are not signatures or
complete-source or complete-target hashes. The summary explicitly
reports `FullSource` and `FullTarget` as unavailable. Reproducing a digest requires retaining the same
inputs: crosswalks can later change or be removed by rollback, and the source snapshot itself is not retained
by the summary. An unkeyed digest can reveal equality and may be guessable for small or low-entropy datasets;
it is not anonymization. Restrict audit access and protect retained source/evidence accordingly.

* `SourceProjection` (`ss-source-projection-v1`) covers every row actually fetched by `SsSnapshotReader`
  for that execution, including rows later accepted, rejected or excluded, and dependency rows. The selected
  tables are the writer's `Tables` arrays: MyTeam reads Region, Location, LookupCode, LookupSortOrder, Role,
  User, UserRole, SheriffActingRank, SheriffAwayLocation and SheriffLeave; Training reads LookupCode, User,
  SheriffTraining and SheriffStandardTraining; Scheduling reads LookupCode, Assignment, Duty, DutySlot and
  Shift. Applied Report reads their union. Every dry run reads the union. MyTeam and all dry runs also include
  the derived `__UsersWithHistory` ID-only result. Each regular row covers the reader's `Id`, `CreatedOn`,
  `UpdatedOn` and precisely the table-specific `Fields` projection in `SsSnapshotReader`; it excludes photo
  bytes, replacing them with `HasPhoto`, and excludes Region.Code. It does not cover unselected tables,
  columns, rows not returned by the reader, independent oracle captures, or photo contents. It is not a
  hash of the whole source. It is unavailable if no complete reader result was recorded, or if attempts
  for one run fetched differing projections.
* `OutcomeEvidence` (`ss-outcome-evidence-v1`) covers every persisted `DataMigrationOutcomes` row for this
  run, as `(EntityType, LegacyId, Outcome, Reason)`, including accepted, rejected, excluded and auxiliary
  outcomes. It fingerprints those classifications, not the underlying source or target payloads.
* `AppliedCrosswalkEvidence` (`ss-applied-crosswalk-evidence-v1`) covers the SS `DataMigrationRecords` whose
  `LastAppliedRunId` equals this run, as `(EntityType, LegacyId, TargetType, TargetKey, PayloadHash,
  AfterHash, IsDeleted)`. The existing payload/after hashes cover their respective mapped/allowed fields;
  they do not cover every source or target column or photo bytes. Unchanged existing crosswalks not assigned
  to this run, rejected/excluded rows without crosswalks, and mutation-only alias refreshes may be absent.
  An empty digest means no matching persisted crosswalks, not an empty source or target. It is not applicable
  to dry runs.
* `SimulationCandidates`, when a completed simulation manifest exists, is the existing manifest's
  `CandidateHash` and `CandidateCount` (manifest format version 2). It hashes the JSON array of
  `(EntityType, LegacyId, TargetType, TargetKey, PayloadHash)` candidates in .NET ordinal entity/key order,
  using SHA-256. Candidates are simulated mappings, not applied business rows or a target-content digest.
  Failed/incomplete simulations and simulation Report may have no manifest, in which case it is unavailable.

For the first three digests, the byte stream starts with the UTF-8 format name as a field. A field is a
signed 32-bit big-endian byte length followed by UTF-8 bytes; null is length -1. Counts are signed 64-bit
big-endian. Source tables and row IDs use .NET ordinal ordering: after the table count, each table name,
row count, and each row ID and `System.Text.Json`-serialized `SsRow.SimulationInput` are framed in that
order. Its projected dictionary keys use .NET ordinal order, with native JSON value types and no Unicode
normalization. Outcome and crosswalk rows use PostgreSQL `C`/UTF-8 byte order on `(EntityType, LegacyId)`;
their listed fields are framed in order (IsDeleted is `1` or `0`), followed by the total row count.
Empty tables and empty evidence therefore have defined digests. These formats must be versioned if the
projection, ordering, serialization or framing changes.

## Data Protection

Ordinary responses/findings/logs expose only safe keys, classes, counts and metrics.
Never return connection strings, credentials, photos, protected source comments or raw errors.
SS worker technical failures retain bounded exception types, PostgreSQL SQLSTATE and safe identifier
metadata, and migration stack frames in stderr and migration-only failure evidence; exception messages,
detail and unapproved hints are excluded, and the worker exception does not chain the original error.
Rollback impact values are intentionally sensitive: separate restricted storage/download,
audited access and retention; not ordinary findings or a public report.
No automatic backup restore, source writes or production enablement.
