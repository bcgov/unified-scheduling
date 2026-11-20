# SS Bulk Persistence Mode (Training, Scheduling)

Scheduling has its own section: [Scheduling Bulk](#scheduling-bulk). Everything before it describes
Training. The shared contract (trigger binding, executor, fencing, TEMP privilege) applies to both jobs.

**Opt-in; default unchanged.** `DataMigration:Sources:SS:Jobs:Training:PersistenceMode` selects how the
Training job persists accepted rows:

| Value | Behaviour |
|---|---|
| `PerRow` (default) | Existing writer: per-row `SaveChanges`, savepoints, rollback journal capture. |
| `Bulk` | Same classification/mapping rules (`SsTrainingRules`), persisted per chunk with binary COPY staging and set-based SQL. **No rollback journal is captured.** |

`DataMigration:Sources:SS:Jobs:Training:BulkChunkSize` (default `2000`) is the approximate number of source
history rows per chunk transaction. A user's histories, including users linked by an owned-target rebind, are
never split across chunks. The validator refuses `Bulk` on any job other than Training and Scheduling (even
when that job is disabled), an undefined mode, and a non-positive chunk size.

Bulk runs through the same Hangfire trigger and `SsMigrationExecutor`. Nothing else changes: no new host or
container, the same audited API acquisition and actor, source/job lineage (latest MyTeam must be Completed), the
per-run advisory lock, control fencing (pause/abort/cancel are checked before and after every chunk body),
heartbeat, run counters and the terminal-run guard. The mode is **bound at trigger time**: the trigger audit
evidence records `PersistenceMode`. If the configuration changes before execution, the run is refused with
`SS queued run configuration changed.` Dry runs use the existing `SsDryRunSimulation` path in either mode and
persist no business rows.

## Per-chunk transaction

Each chunk is one transaction:

1. Fence: the run is still `Running`, with no pause or abort control for `SS` or `SS:Training`.
2. `CREATE TEMP TABLE ss_bulk_* ... ON COMMIT DROP`, then Npgsql binary COPY of the staged rows.
3. Set-based `INSERT`/`UPDATE` of business rows (`Trainings`, `TrainingCategories`,
   `TrainingMandatoryTrainingProfiles`, `UserTrainings`, the `Users.TrainingProfileId` compose).
   - Identity values are pre-allocated from the column sequence and inserted explicitly.
   - Generated IDs are mapped to source keys by explicit key join, never by `RETURNING` order.
4. Crosswalk rows (`DataMigrationRecords`) and receipts (`DataMigrationMutations`).
   - `ON CONFLICT DO NOTHING` runs against the unique `(Source, EntityType, LegacyId)` index.
   - A conflict aborts the chunk instead of creating a duplicate.
   - Reruns and crash-resumes anti-join on existing crosswalks, so they insert nothing twice.
5. Outcomes, rejection reasons, retry work items, checkpoints, counters and heartbeat.
6. Fence again, then commit.

Invalid rows and their dependants are rejected in memory before any accepted row is written. Rejections use the
same reasons and evidence tables as PerRow. A failure inside a chunk rolls back the whole chunk, including its
ledger changes; earlier chunks stay committed and the next approved run resumes from the crosswalk.

Migration execution intentionally does **not** create a general `AuditRecord` for each business or
bookkeeping row, in either mode. The Bulk COPY audit path observes the migration context's disabled flag.
One terminal run summary is written instead, in addition to the separate operator-action audits. This is
an accepted loss of granular general audit, not per-row audit parity. Ordinary application saves remain
audited. See the [run summary contract](contracts/migration-operations.md#run-audit-summary) for the exact
fingerprints and their limitations. Per-record outcomes, crosswalks, mutations, controls and PerRow rollback
journals are unchanged. When the context has `AuditableEntityInterceptor` (production), Bulk still stamps
`CreatedById`/`UpdatedById` with the System user like background PerRow saves.

Raw staging commands and COPY operations inherit the migration command timeout
(`DataMigration:CommandTimeoutSeconds`, or the target connection string's `Command Timeout`), the same as EF
commands. A mapped note that exceeds the 2000-character target column (counted in characters, like PostgreSQL
`varchar`) fails only that row (`TargetWriteFailed:DbUpdateException`, as PerRow does), not the chunk. Like
PostgreSQL, Bulk accepts a longer note when every character past 2000 is an ASCII space; the stored value is
truncated in both modes.
Save rules (`ISaveRule`) are not invoked. The only relevant rule, `TrainingProfileTypeExistsRule`, checks that a
requirement's profile type exists. Bulk resolves that profile type from the target, and the foreign key enforces
the same thing.

## Database privilege: TEMP

Staging uses transaction-scoped temp tables, so the migration target role needs `TEMP` on the target database.
PostgreSQL grants `TEMP` to `PUBLIC` by default; if that was revoked, grant it explicitly:

```sql
GRANT TEMP ON DATABASE <unified_db> TO <migration_role>;
```

Without it, the first Bulk chunk fails with `permission denied to create temporary tables` and nothing from that
chunk is committed.

## Undo (no rollback journal)

Bulk runs have `RollbackJournalKeyId = NULL`, so `SsRollbackService` refuses them with
`MissingOriginalRollbackJournal`. Use the run-scoped crosswalk undo instead:

```csharp
var result = await new SsBulkUndo(dbContext).UndoAsync(runId, ct);
// result.Blockers empty => result.ReceiptRunId is the new "Rollback" receipt run.
```

`SsBulkUndo` runs in one transaction under the migration advisory lock (`872,1`). It then takes
share-row-exclusive locks on the crosswalk tables and the undone job's tables only (Training: users and training
tables; Scheduling: scheduling tables, parents first), through the same bounded, back-off-first acquisition as
Scheduling chunks: each attempt is cut off below `deadlock_timeout` and retried, so an application transaction
that waits on undo is never aborted as a deadlock victim. It **refuses with blockers and changes nothing** when
any of these apply:

- another SS run is `Running` (checked right after the advisory lock and before any table lock, so undo never
  waits on an active chunk);
- the run is not Training or Scheduling (Scheduling undo is described under [Scheduling Bulk](#scheduling-bulk));
- the run is a dry run, already rolled back, or still running;
- the run has a journal, or its trigger evidence did not bind `PersistenceMode=Bulk`;
- a later Training run or dependent Scheduling/Report run is still live;
- the run recorded anything other than `Insert`/`Link`/`Compose` receipts;
- it updated pre-existing crosswalks;
- any inserted or composed target changed since the run (its `xmin` differs from the receipt version);
- another history references a course the run created;
- a profile requirement on a run-created course was added or edited after the run
  (`CreatedCourseRequirementsChanged`);
- a category the run created was edited after the run (`CreatedCategoryChanged`) or is now used by another
  course (`CreatedCategoryHasLaterDependents`).

Categories and requirements carry no receipt. The run inserts them in the same chunk transaction as its
courses, so a row is treated as run-created and unchanged only when its `xmin` equals the course receipts'
version.

Otherwise it:

1. clears `Users.TrainingProfileId` for users the run composed;
2. deletes the `UserTrainings` and `Trainings` (with their profile requirements) the run inserted, plus
   the verified categories the run created for those courses;
3. deletes the run's mutations and the crosswalk rows it created or linked;
4. marks the run `RolledBack` and writes a `Rollback` receipt run, a `RollbackProof` outcome, and
   `RollbackRefresh` receipts for the restored MyTeam user versions.

Run outcomes, work items and checkpoints are kept as evidence. `Users.UpdatedOn/UpdatedById` of composed users
are not restored, because there is no before-image.

Equivalent SQL for review only; use the service, because it also checks the blockers and versions. `:run` is
the Bulk run id:

```sql
BEGIN;
SELECT pg_advisory_xact_lock(872,1);
CREATE TEMP TABLE undo_m ON COMMIT DROP AS
  SELECT m."Operation", r."Id" record_id, r."TargetType", r."TargetKey"
  FROM "DataMigrationMutations" m JOIN "DataMigrationRecords" r ON r."Id" = m."DataMigrationRecordId"
  WHERE m."RunId" = :run;
UPDATE "Users" SET "TrainingProfileId" = NULL
  WHERE "Id"::text IN (SELECT "TargetKey" FROM undo_m WHERE "Operation" = 'Compose' AND "TargetType" = 'User');
DELETE FROM "UserTrainings"
  WHERE "Id"::text IN (SELECT "TargetKey" FROM undo_m WHERE "Operation" = 'Insert' AND "TargetType" = 'UserTraining');
DELETE FROM "TrainingMandatoryTrainingProfiles"
  WHERE "TrainingId"::text IN (SELECT "TargetKey" FROM undo_m WHERE "Operation" = 'Insert' AND "TargetType" = 'Training');
CREATE TEMP TABLE undo_c ON COMMIT DROP AS
  SELECT DISTINCT c."Id" FROM "TrainingCategories" c JOIN "Trainings" t ON t."TrainingCategoryId" = c."Id"
  WHERE c."CreatedOn" = (SELECT "StartedOn" FROM "DataMigrationRuns" WHERE "Id" = :run)
    AND t."Id"::text IN (SELECT "TargetKey" FROM undo_m WHERE "Operation" = 'Insert' AND "TargetType" = 'Training');
DELETE FROM "Trainings"
  WHERE "Id"::text IN (SELECT "TargetKey" FROM undo_m WHERE "Operation" = 'Insert' AND "TargetType" = 'Training');
DELETE FROM "TrainingCategories" WHERE "Id" IN (SELECT "Id" FROM undo_c);
DELETE FROM "DataMigrationMutations" WHERE "RunId" = :run;
DELETE FROM "DataMigrationRecords"
  WHERE "Id" IN (SELECT record_id FROM undo_m WHERE "Operation" IN ('Insert','Link'));
UPDATE "DataMigrationRuns" SET "Status" = 'RolledBack' WHERE "Id" = :run;
COMMIT;
```

## Proof

`Unified.Tests.DataMigration.SsTrainingBulkPostgresTests` is gated by
`UNIFIED_MIGRATION_ISOLATED_TEST_CONNECTION_STRING` and the local source schema evidence. It covers:

- Existing PerRow-vs-Bulk comparisons exercise four adverse rounds on template clones, after real MyTeam runs
  (round 4 adds oversized-note row failures). Business rows, outcomes, reasons, failures, crosswalks and
  receipts remain comparison surfaces. Audit assertions check the separate trigger, one terminal summary
  per run and the absence of per-row migration audit; they do not establish granular audit parity.
- Concurrency, terminal-run, crash, abort and cancel resume.
- Dry run, mode binding, undo-to-baseline, and undo blocked by edited categories/requirements.
- An opt-in benchmark: `UNIFIED_MIGRATION_BULK_BENCHMARK_HISTORIES=<n>`.

Local benchmark: .NET 10 SDK container against a PostgreSQL 16 container on one developer workstation, with
20,101 scanned Training source rows, chunk size 2000 and auditing disabled in both modes. PerRow took 1881.6 s
(about 11 rows/s); Bulk took 10.0 s (about 2,013 rows/s). Outcomes, run counters and business state matched.
Absolute numbers depend on hardware and network latency; the ratio comes from removing per-row round trips.

## Scheduling Bulk

**Opt-in; default unchanged.** `DataMigration:Sources:SS:Jobs:Scheduling:PersistenceMode=Bulk` with
`DataMigration:Sources:SS:Jobs:Scheduling:BulkChunkSize` (default `2000`) writer units per chunk transaction.
`BatchSize` keeps its PerRow meaning (tracker save + clear between units) and is mirrored in Bulk so rule
behaviour that depends on tracker state is identical. A writer unit is a whole merge group, duty graph, series
or captured component, so a chunk never splits a graph.

### Design: the existing rules, bulk writes

Bulk does not re-implement Scheduling. The unchanged rule code (`SsSchedulingWriter`, `SsSchedulingGraphReplay`,
`FamilyExpansion`, `SsAssignmentSeriesWriter`, `SsWriteSession`) now talks to the target through two narrow
interfaces (`ISsSessionBackend`, `ISsSchedulingStore`):

- **PerRow:** `SsSchedulingEfStore`/`SsEfSessionBackend` keep the original EF queries, `SaveChanges`,
  savepoints and rollback journal. Business writes are unchanged; general per-row migration audit is
  replaced by the run summary described above.
- **Bulk:** `SsSchedulingSim` loads one consistent snapshot of the modelled target tables into memory and plays
  the database for the same rules:
  - real ids from the table sequences;
  - the audit interceptor's actor stamps;
  - the `SchedulingConcurrencyInterceptor` root version bumps: a save that adds or changes a shift user, link,
    link user or scheduling event touches the owning unchanged `ShiftEntry`/`AssignmentEntry`/`AssignmentSeries`
    (`UpdatedOn`/`UpdatedById` updated, version advanced). In an audited context an interceptor-only touch
    is not audited because Audit.NET snapshots the change set before interceptors run; SS migration emits
    no per-row audit in either case. It applies only when the context has that interceptor (production does), and a
    touch of an unmodelled `ShiftSeries` is refused (`UnmodelledConcurrencyRoot:ShiftSeries`);
  - PostgreSQL value normalisation;
  - not-null, varchar length, unique-index, foreign-key and named check constraints;
  - savepoint rollback of rejected units.

  Outcomes, rejection reasons (shift merging, nested/touching intervals, historical gaps, future gaps,
  cancellations, duty/slot graphs, user links, ownership, recurrence/series expansion) are therefore produced
  by the same code in both modes.

`SsSchedulingBulkWriter` first runs a complete **no-write preflight pass**. If any path Bulk cannot reproduce
exactly is reached, the run fails before the first business write with a `SchedulingBulk` outcome
`Failed:BulkRequiresPerRow:<reason>`. The operator then runs the same job in `PerRow`. The real pass reloads the
target and refuses with `TargetChangedDuringBulkRun` unless its snapshot digest equals the one the preflight
validated, so a refusal can never surface after earlier chunks committed. The real pass persists each chunk in
one transaction through `SsBulkChunkRunner`:

1. Fence: the run is still `Running`, with no pause or abort for `SS` or `SS:Scheduling`.
2. Lock the read-only rule inputs (`SHARE`: `Users`, `StatCategories`, `SubCategories`,
   `ShiftAssignmentSeriesLinks` and the principal tables referenced by foreign keys), then the modelled tables,
   `DataMigrationRecords` and `DataMigrationMutations` (`SHARE ROW EXCLUSIVE`). This is the order a Training
   chunk or an application transaction uses (users before crosswalks or scheduling rows). Each attempt is bounded
   below `deadlock_timeout` and then rolls back to a savepoint, releasing its locks, and retries after a short
   back-off (up to 10 minutes; then the chunk fails and the run is resumable). In any lock wait cycle the Bulk
   run therefore yields; PostgreSQL never aborts the other transaction. Refuse with `TargetChangedDuringBulkRun`
   if anything outside this run changed them since the snapshot or the previous chunk (digest check). The
   read-only inputs are digested by content (user names and IDIR, stat-category groups,
   series links, principal keys), so unrelated column updates elsewhere do not refuse the run. The `SHARE` locks
   block application writes to those tables only for the duration of each chunk transaction.
3. Binary COPY of new rows: `CourtRooms`, `AssignmentDefinitions`, `EventSeries`, `Events`, `AssignmentSeries`,
   `ShiftEntries`, `ShiftEntryUsers`, `AssignmentEntries`, `ShiftAssignmentEntries`, `ShiftAssignmentEntryUsers`
   and `DataMigrationRecords`. Ids are pre-allocated and mapped by key, never by `RETURNING` order.
   Same-chunk updates are applied through temp staging tables.
4. Receipts (`DataMigrationMutations`, stamped with the chunk transaction id as the target version), outcomes,
   work items, checkpoints, counters and heartbeat. Per-row general audit is disabled for migration execution;
   the run summary is committed with terminal state.
5. Fence again, then commit.

Crosswalk uniqueness (`Source, EntityType, LegacyId`) is the idempotency fence. Committed crosswalks are
`ExistingCrosswalk`/`ExistingGroupCrosswalk` on the next run, exactly as for PerRow.

### Refusals (Bulk ? `BulkRequiresPerRow:<reason>`, nothing written)

| Reason | When |
|---|---|
| `OwnedUpdate:<type>`, `OwnedGroupUpdate:<type>` | A source row already migrated changed, and PerRow would update its owned target in place. This includes an `UpdatedOn`-only touch of a merged group. |
| `GraphReplacement` | A changed or extended scheduling component requires deleting and rebinding an existing graph. This covers a source change to an existing shift and a new future duty joining an existing recurrence component. PerRow without a rollback journal rejects these as `GraphReplacementRequiresInverseJournal`, so Bulk refuses the whole run instead of reporting a different per-row outcome. |
| `SchedulingBusinessValidatorsRegistered` | An `ISsMigrationBusinessValidator` is registered. Validators read the committed EF target inside the PerRow transaction, which the in-memory image cannot reproduce. |
| `TargetChangedDuringBulkRun` | Someone wrote the modelled target tables during the run. |
| `TrainingRunInProgress` | An SS Training run is `Running` when the Scheduling Bulk run starts. Both write `Users`/crosswalks; run them one after the other. A Training run started *during* a Scheduling Bulk run stops it at the next chunk with `TargetChangedDuringBulkRun` (committed chunks are complete; rerun Scheduling afterwards). |
| `Delete:*`, `MutationUpdate`, `Unmodelled*`, `Unsupported*`, `NonUtcOffset`, � | Schema or rule paths the simulation does not model. These guard against future rule or schema changes. |

The findings API and CSV show the `SchedulingBulk` outcome and these reasons as-is (compiled vocabulary in `MigrationDiagnostics`); any detail after the reason (an EF type or index name) is shown only as an opaque `sha256:` key.

**In short, Bulk is for loading new Scheduling rows and resuming.** It covers:

- the first full load;
- resuming after a cancelled, aborted or failed PerRow or Bulk run;
- reruns that add rows forming new graphs.

Deltas that change already-migrated rows must run in PerRow.

### Mixed-mode resume (PerRow ? Bulk)

A Bulk run may resume a Canceled/Failed PerRow run of the same job. The lineage gate only requires a Completed
MyTeam run with the same `DryRun`/`HistoryStartDate`. PerRow commits whole units per batch, so a canceled PerRow
run never leaves a half graph. Bulk reads committed crosswalks and receipts and treats those rows exactly like
a PerRow rerun would: same outcomes and counters, no duplicate targets or crosswalks. Proven by
`MixedMode_PerRowCanceledThenBulk_EqualsFullPerRow_AndReverse`:

- PerRow is cancelled at 21 of 42 graph crosswalks.
- Bulk resume equals a PerRow resume of an identical copy (all ledger and business sections).
- The final state equals a full PerRow run.
- The reverse (Bulk crash after 2 chunks, then PerRow resume, versus Bulk resume) is also equal.

### Undo (no rollback journal)

Bulk Scheduling runs have no rollback journal. `SsBulkUndo.UndoAsync(runId)` (same entry point, lock and
general blockers as Training) removes exactly what the run created:

- inserted `CourtRooms`, `AssignmentDefinitions`, `AssignmentSeries` (and their `EventSeries` and occurrence
  `Events`), `ShiftEntries`, `AssignmentEntries` and their events;
- shift users, shift-entry links and link users;
- the run's receipts and crosswalks.

It then marks the run `RolledBack` and writes a `Rollback` receipt run with a `RollbackProof` outcome. Linked
pre-existing targets (e.g. an existing court room) are only unlinked. Deletes are raw SQL without audit rows,
like Training undo.

Scheduling-specific blockers (nothing is changed):

- `BulkUndoRequiresInsertLinkComposeOnlyRun`: a receipt other than Insert/Link/Compose/SealNewGraph/Repartition.
- `RunTouchedPreExistingCrosswalk`: a crosswalk the run touched has receipts from another run. This covers a Bulk
  run that resumed a PerRow run.
- `RunUpdatedPreExistingCrosswalk` and `RunModifiedPreExistingTarget`: graph-shaping receipts on rows the run did
  not insert, or any row outside the owned set carrying one of the run's chunk transaction ids. `Link` receipts
  to pre-existing rows (e.g. a sub-category written by another transaction) are not version-checked.
- `OwnedTargetDrift`: an owned row (including child events and users) no longer carries one of the run's chunk
  versions, i.e. it was edited, or a row was added under an owned shift. The graph fingerprint of every owned
  crosswalked target is also recomputed and compared with the crosswalk's recorded hash, which catches a later
  **delete** of a child row (shift user, shift-entry link or link user) that leaves no version behind. This costs
  one fingerprint read per owned target, so undo of a large run takes minutes.
- `InsertedTargetMissing` and `InsertedTargetHasLaterDependents`: later rows reference owned rows, including
  set-null references that would silently detach them.
- `LaterSameJobRunMustRollbackFirst`, `DependentJobsMustRollbackFirst` and `SourceJobRunning` (shared). An undo
  requested while a chunk is live returns `SourceJobRunning` without waiting on chunk locks.

### Known differences and gaps

- No rollback journal. Bulk runs are refused by `SsRollbackService` (`MissingOriginalRollbackJournal`); use
  `SsBulkUndo`.
- `TEMP` privilege is required (see above).
- **Quiet target required.** Each chunk transaction blocks application writes to the Scheduling tables (and
  `SHARE`-locks users, stat categories and FK principal tables) for its duration, typically seconds. Any
  application write to the modelled Scheduling tables during the run stops it with `TargetChangedDuringBulkRun`
  (safe and resumable, but the remaining rows are not loaded). Run Scheduling Bulk in the cutover window with
  application writes to scheduling paused; otherwise use PerRow.
- `ISsMigrationBusinessValidator` is not registered in this pipeline. Both modes therefore record
  `SchedulingValidation: NeedsAction ActualSchedulingReadValidatorUnavailable`. Working-hours, conflict and
  publication validation is **not** performed by the migration in either mode. This remains a documented no-go
  condition for production publication and needs independent validation after load.
- Memory: Bulk holds the modelled target Scheduling tables and the run's writes in memory for the run.
- Existing PerRow behaviour preserved as-is by Bulk, not fixed:
  - a reversed-interval shift is migrated;
  - failed material slot links (`Failed:MaterialMissingShiftUserLink`) keep their crosswalk links.

### Proof

`Unified.Tests.DataMigration.SsSchedulingBulkPostgresTests` is gated by
`UNIFIED_MIGRATION_ISOLATED_TEST_CONNECTION_STRING` and `UNIFIED_MIGRATION_SCHEDULING_SOURCE_TEMPLATES` (frozen
synthetic source databases). Each test clones one seeded target with a genuinely completed MyTeam run, runs
PerRow and Bulk on separate clones, and diffs both ways with ids normalised through crosswalks. The diff covers:

- runs/counters, outcomes and reasons, work items, checkpoints;
- crosswalks and receipts, failures;
- every Scheduling business table (shifts, events, series, assignment entries, links, users);
- business-table audit slices; the retained-template test also asserts that a Bulk run writes no per-row audits and
  exactly one `DataMigrationRunSummary` audit (`EntityPK` = run id) with nonzero counts.

Tests:

- **Retained synthetic templates**: round 1 plus rerun.
- **Deterministic adverse cases** on the ready template:
  - touching, nested and gapped shifts;
  - another user overlapping;
  - reversed intervals;
  - the DST fall-back night;
  - future gaps and future unassigned slots;
  - a duty without assignment (rejected parent) with its slot;
  - flag-only slots;
  - a linked-only pre-existing court room.

  Round 2 covers target drift, a room rename and a new graph, with parity. Round 3 covers source changes to
  migrated rows plus a new future duty: Bulk refuses, then PerRow on both clones converges.
- **Safety**:
  - duplicate trigger and duplicate execute refused;
  - undo during a live chunk refused within 30 s;
  - a lock cycle with a concurrent writer (Users then a scheduling table) on a chunk: Bulk backs off, nobody is aborted (fails with the old lock order: verified negative control);
  - a Training run in progress refuses Bulk; a mid-run target change refuses with nothing written;
  - crash, abort and cancel between chunks, each resuming to the reference;
  - terminal run ids refused;
  - rerun inserts 0.
- **Dry run**, mode binding, undo to baseline, re-apply, and undo refused after drift, a later shift user or a
  deleted owned link. Undo completes under lock cycles with an application writer in both table orders (the
  previous unbounded child-first locking deadlocked: verified negative control).
- All Scheduling PostgreSQL tests use the production interceptor chain (`SchedulingConcurrencyInterceptor`,
  then `AuditableEntityInterceptor`).
- **Mixed-mode resume** (above).
- **Opt-in benchmark**: `UNIFIED_MIGRATION_SCHEDULING_BENCHMARK_TEMPLATE=<source db>` (and
  `UNIFIED_MIGRATION_SCHEDULING_BENCHMARK_PERROW=false` to skip the PerRow leg).
