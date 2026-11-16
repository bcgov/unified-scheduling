# SS implementation status — 2026-11-16

**Partial implementation, NOT production-ready and NOT a migration acceptance pass.**

Planning snapshot: [ss-implementation-baseline](ss-implementation-baseline/README.md).
Workspace planning artifacts remain canonical. Initial planning commit: `b589bee4`.
Writer/lineage implementation commit: `e16b559b`.
Final supplied-gender/IANA validation guard commit: `7287236`.

## Implemented and exercised

- Four job identities, feature/source/per-job enable and cron gates; old shell schedule cleanup.
- Required fixed 2026-12-01 cutover, explicit-false JC expiry preflight, source-wide and
  per-job migration controls, transaction-scoped PostgreSQL dependency gate and run-row fence.
- Training and Scheduling can execute concurrently after the same applied MyTeam run.
  Running Report blocks new dependent jobs. Failed/latest MyTeam cannot unlock dependants.
- Migration-only EF state: Job, MyTeam lineage, cutover, ownership/structural after hashes,
  unique Running `(Source, Job)`, and one outcome per run/entity/source ID.
- Source explicit projections, typed numeric/UUID paging, repeatable-read/read-only snapshot.
  A dedicated read-only source role is used in the Docker rehearsal.
- Real tracked EF inserts and actual FK resolution for regions, locations, positions,
  Type-5 allowlist, roles, users, UserRole, acting, standalone leave, training catalogue,
  categories/profile links/history, rooms, definitions, merged shifts, duties and slot/user links.
- References are add/link-only. Source audit fields and SS permissions are not imported.
  Users use generated GUIDs and normalized IdirName; no user-created/renewal/email signals.
- Training certificate expiry and completion instants are not recalculated.
  Person null timezones stay null where the target has a corresponding field.
- Gap-free shifts merge; historical gap blocks stay separate; future gap blocks are held.
  Future existing-shift/assignment conflicts reject, and material missing-shift links fail.
- Real-DML MyTeam dry run rolls back business targets and crosswalks, then persists safe outcomes.
- CLI exports call the actual `UserTrainingReportQueryHandler` and `WorkingHoursService`,
  not copied report/overtime formulas. Output omits names, notes, descriptions, photos and SSO fields.
- Explicit stale-run recovery has a NOWAIT run-row lock and fence. **Not yet independently tested.**

See [measured rehearsal](reports/2026-11-16-rehearsal.md) for run IDs and counts.

## Acceptance blockers / exact remaining work

1. **Away locations:** target migration `20261111005725_RemoveUserAwayLocations` removed
   the required user/detail model. The adverse fixture has 183 selected away rows, all
   NeedsAction. No feature was restored and no away fact was relabelled as a loan-out.
   Resolve the contract with the application owner; do not silently discard these facts.
2. **Full Q-X4 updates:** changed non-reference sources currently reject
   `OwnedUpdateRequiresReview`. Structural source and target digests are distinct, but
   there is no complete mapped-field, text-drift-aware compare-and-update implementation.
   Linked-only rows never update. This is safe preservation, not completed incremental migration.
3. **Progress/backfill:** full source snapshots are rescanned. There is no durable per-table
   bounded incremental cursor, independent rejected-row queue or resumable earlier-cutoff backfill.
   BatchSize bounds source fetches, **not target commit size**. Target writes currently use one
   job transaction and row/group savepoints; memory/runtime/table-lock duration are not production-fit.
4. **Dry-run lineage:** MyTeam supports an isolated target with no live SS crosswalks.
   Training/Scheduling dry runs explicitly reject missing isolated parent candidates.
   Candidate staging/rehydration and cross-job dry-run parity are not implemented.
5. **Scheduling recurrence:** G10 qualification and EventSeries/assignment/shift series inserts
   are not implemented. Imported entries are standalone. A scheduling validation NeedsAction
   remains even when all selected primary rows transfer.
6. **Scheduling refinements:** singleton definition defaults currently derive from observed duties,
   not always the Assignment Start/End interval. Exact reviewed Type1–4 classification coverage is
   incomplete; a frozen exact subset plus documented C1/3 logical fallback is used, with findings.
   Sergeant fallback is C37/135 with a finding. Full historical FTE/conflict reporting and all
   cancellation/partial-outside edge cases still need independent validation.
7. **Rollback:** restricted impact preview, run-bound confirmation digest, reverse dependency guards,
   merged-target membership proof, later-edit/dependent-row export, inverse execution and atomic
   footprint/progress reset are **not implemented**. Multi-entry duties also need a complete
   root-target ownership ledger before rollback can be safe. There is no automatic DB restore.
8. **Report:** the report job is intentionally non-passing until actual-query comparison,
   complete source coverage, classification approval boundaries and thresholds are integrated.
   It does not emit a fabricated reconciliation success. Current CLI metrics are operational
   evidence, not the final acceptance report.
9. **Actual source/target parity:** target training and WorkingHours queries ran on the small ready
   fixture. The legacy oracle is owned by the separate legacy harness. Full matched exports,
   O2–O4 target queries/distribution capture, production-approved OT bounds and all business-date
   comparisons are still pending. No production 84-day constant is used to claim synthetic parity.
10. **Training application gaps:** UserTraining has no per-record timezone (BL14), and future
    expiry-policy differences (BL13) must be measured, not fixed by shifting historical instants.
    No app schema or training behavior was changed.
11. **Synthetic validity:** adverse source generation succeeded and the manifest is retained.
    SH ±20% fit, all EX/LX/AG/AD/SP/OD/SH extractors, repeat-build structural hashes and a full
    applied adverse-fixture rehearsal have not passed. The copied generator uses run-clock values
    for some fields; reproducibility needs a defined stable anchor/field policy.
12. **UnitTests:** existing tests still construct/assert the obsolete blocked-shell contract.
    The attempted Unified test run failed compilation (two CS1729 errors); **zero tests executed**.
    UnitTests must update those tests and add PostgreSQL concurrency, adverse, rollback, replay,
    drift, paging, timezone, versioning and coverage tests. No tests were skipped or fake-passed.

The existing pre-request deletion `docs/data-migration/ss-mapping-contracts.md` remains
unstaged. No tenant GitOps, web generated client, application auth/scheduling/training behavior,
JC code default or shared app database was changed. No push was performed.
