# SS Training Bulk Persistence Mode

**Opt-in; default unchanged.** `DataMigration:Sources:SS:Jobs:Training:PersistenceMode` selects how the
Training job persists accepted rows:

| Value | Behaviour |
|---|---|
| `PerRow` (default) | Existing writer: per-row `SaveChanges`, savepoints, rollback journal capture. |
| `Bulk` | Same classification/mapping rules (`SsTrainingRules`), persisted per chunk with binary COPY staging and set-based SQL. **No rollback journal is captured.** |

`DataMigration:Sources:SS:Jobs:Training:BulkChunkSize` (default `2000`) is the approximate number of source
history rows per chunk transaction. A user's histories, including users linked by an owned-target rebind, are
never split across chunks. The validator refuses `Bulk` on any job other than Training (even when that job is
disabled), an undefined mode, and a non-positive chunk size.

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

Business-table `AuditRecords` are written by COPY with the same shape as Audit.NET: Added/Modified, the System
actor, and the same column exclusions. Migration ledger tables (`DataMigration*`) are not audited in Bulk mode.
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

`SsBulkUndo` runs in one transaction under the migration advisory lock (`872,1`) with share-row-exclusive locks
on the affected tables. It **refuses with blockers and changes nothing** when any of these apply:

- another SS run is `Running`;
- the run is not Training;
- the run is a dry run, already rolled back, or still running;
- the run has a journal, or its trigger evidence did not bind `PersistenceMode=Bulk`;
- a later Training run or dependent Scheduling/Report run is still live;
- the run recorded anything other than `Insert`/`Link`/`Compose` receipts;
- it updated pre-existing crosswalks;
- any inserted or composed target changed since the run (its `xmin` differs from the receipt version);
- another history references a course the run created.

Otherwise it:

1. clears `Users.TrainingProfileId` for users the run composed;
2. deletes the `UserTrainings` and `Trainings` (with their profile requirements) the run inserted, plus
   categories created at the run start that are now unreferenced;
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
DELETE FROM "Trainings"
  WHERE "Id"::text IN (SELECT "TargetKey" FROM undo_m WHERE "Operation" = 'Insert' AND "TargetType" = 'Training');
DELETE FROM "TrainingCategories" c
  WHERE c."CreatedOn" = (SELECT "StartedOn" FROM "DataMigrationRuns" WHERE "Id" = :run)
  AND NOT EXISTS (SELECT 1 FROM "Trainings" t WHERE t."TrainingCategoryId" = c."Id");
DELETE FROM "DataMigrationMutations" WHERE "RunId" = :run;
DELETE FROM "DataMigrationRecords"
  WHERE "Id" IN (SELECT record_id FROM undo_m WHERE "Operation" IN ('Insert','Link'));
UPDATE "DataMigrationRuns" SET "Status" = 'RolledBack' WHERE "Id" = :run;
COMMIT;
```

## Proof

`Unified.Tests.DataMigration.SsTrainingBulkPostgresTests` is gated by
`UNIFIED_MIGRATION_ISOLATED_TEST_CONNECTION_STRING` and the local source schema evidence. It covers:

- PerRow-vs-Bulk parity over three adverse rounds on template clones, after real MyTeam runs. The comparison is
  normalized through crosswalks and covers outcomes, reasons, failures, business rows, crosswalks, receipts and
  audit rows.
- Concurrency, terminal-run, crash, abort and cancel resume.
- Dry run, mode binding and undo-to-baseline.
- An opt-in benchmark: `UNIFIED_MIGRATION_BULK_BENCHMARK_HISTORIES=<n>`.

Local benchmark: .NET 10 SDK container against a PostgreSQL 16 container on one developer workstation, with
20,101 scanned Training source rows, chunk size 2000 and auditing disabled in both modes. PerRow took 1881.6 s
(about 11 rows/s); Bulk took 10.0 s (about 2,013 rows/s). Outcomes, run counters and business state matched.
Absolute numbers depend on hardware and network latency; the ratio comes from removing per-row round trips.
