# SS implementation status — 2026-11-16

**Implementation is incomplete. This is NOT a production or migration acceptance pass.**

Planning mirror: [ss-implementation-baseline](ss-implementation-baseline/README.md).
Workspace artifacts remain canonical. Prior milestones: `b589bee4`, `e16b559b`,
`7287236`, `21ae1f3`; primary follow-ups `67982bc2`, `5bd55720`, runner validation
`edb9c393`, singleton defaults `e29fdd87`. See the [rehearsal record](reports/2026-11-16-rehearsal.md)
for retained failures, run IDs and application-service evidence.
Durable recovery and mapped-update follow-up:
[2026-11-16 recovery evidence](reports/2026-11-16-recovery.md).

## Implemented and exercised

- Four job identities; per-job configuration, source-wide controls, dependency/lineage
  gates, PostgreSQL advisory acquisition lock, Running uniqueness and run-row fencing.
  Report now rechecks its fence and pause/abort controls after reading source snapshots.
- Fixed 2026-12-01 cutover, Vancouver history boundary, explicit-false JC expiry guard.
  CASS remains disabled. No source audit, keyring or permission payload is imported.
- Read-only/repeatable-read source snapshots, typed timestamp/ID paging and explicit
  per-table upper bounds. Server timestamp text is bound back as `timestamptz`, preserving
  finite year-1 seed timestamps instead of converting them to PostgreSQL `-infinity`.
  Complete source snapshots still rehydrate groups; committed work items/checkpoints,
  bounded target batches, heartbeat fencing and replay now persist restart progress.
  Source fetching is not yet optimized to skip pages using those checkpoints.
- Real tracked EF inserts/crosswalks for references, all users, roles/UserRole, acting,
  leave, training catalogue/categories/profile links/history, rooms, definitions,
  merged shifts, duties, slots and qualified assignment series.
- **Administrator mapping fixed:** create/link Sheriff Administrator; newly created
  roles snapshot current Staff permissions once. Missing Sheriff Administrator was a
  mapper bug, not an expected adverse rejection.
- **Away implemented using the current model:** exact source intervals/cancellation
  become LoanOutEntry + Event + one user link, destination in LoanOutLocationId and
  source/home in Event.LocationId. Actual LoanOut-enabled calendar and availability
  services executed successfully. Target availability is day-based; this remains an
  explicit `MigratedWithDifference`, not an assertion of hour-for-hour availability.
  Ordinary create service is intentionally not called: it forces all-day and transfers shifts.
- Qualified G10 draft series use the actual recurrence expander and link existing
  migrated duties only. Every expanded occurrence must match a live source duty;
  no duties are generated. Unsupported patterns remain standalone.
- Singleton definition defaults copy Assignment Start/End even without duties; merged
  definitions retain the most-common observed-duty rule. Real catalogue-only fixture
  transferred 09:00/17:00 exactly.
- Actual SchedulePublishService preview succeeded, then actual publication succeeded
  on a separate persistent clone: two shifts and two assignments. Original ready-v3
  remains unchanged by publication.
- Owned-row updates for users, UserRole, acting, away, leave and training history,
  single-root merged definitions/shifts and single-slot duties:
  require changed mapped source hash, job ownership, unchanged target mapped hash
  and original root/event PostgreSQL row-version proof under row locks. Linked-only
  rows never update. SHA-256 covers mapped text/SSO without retaining/emitting raw values;
  photo bytes are excluded and additionally guarded by source stamps/target versions.
  Training profile and initial assignment-series composition refresh original owner
  concurrency receipts only after verifying unchanged owner-mapped fields.
  Actual text-only updates, new touching member, profile preservation and independent
  target training-note drift rejection ran on the separate probe.
- Independent durable retry rows resolve dependencies below advanced cursors. Earlier
  history has separate resumable backfill progress and matching parent lineage. Actual
  PostgreSQL interruption retained 14 prior batches while rolling back the current one;
  subsequent restart, below-cursor retry and 2014 backfill succeeded.
- Report reconciles one outcome per selected source business row, crosswalk target
  existence and same MyTeam lineage. Group/validation/excluded-table metrics are not
  counted as business coverage. Audit/JcSynchronization/DataProtectionKeys and
  Permission/RolePermission are COUNT-only system/permission exclusions.
- Actual training, scheduling calendar and WorkingHours services feed restricted
  normalized exports. Identity normalization uses crosswalks, not equal source/target IDs.
  Full multiset comparison preserves duplicate multiplicity. Missing fields/surfaces,
  fixture mismatch and primary failures prevent PASS.
- MyTeam dry run performs real DML then rolls back; adverse rollback footprint checked.
- Dedicated runner audit configuration excludes private source strings/SSO/photo values
  while retaining Unified audit identity/action. Earlier isolated synthetic audit copies
  were redacted without deleting business rows, run evidence or audit identities.
- **71 migration tests and 5 dependent Hangfire tests pass, zero failures/skips.**
  These are foundation/preflight tests, not comprehensive PostgreSQL integration coverage.

## Apply safety

**Non-dry-run execution is fail-closed outside exact isolated synthetic database names
and an explicit `AllowUnrecoverableSyntheticApply=true` acknowledgment.** Rollback is
unfinished; do not enable real-environment apply. Publication has a separate exact
clone-name/confirmation guard. No whole-database restore is implemented or attempted.

## Remaining acceptance blockers

1. **Complete mapped updates:** multi-root merge/split/repartition, multi-entry duties
   and changed series membership/qualification replay remain unfinished. Missing
   original version proofs are not retroactively blessed. These cases reject rather
   than partially applying a group or overwriting independent target edits.
2. **Source incremental optimization:** durable retry/backfill/checkpoints and bounded
   atomic target commits are implemented/exercised, but snapshots still rescan all
   source rows for group/identity/dependency reconstruction. Broader concurrency,
   checkpoint and recovery integration coverage remains required.
3. **Dry-run lineage:** no isolated parent candidate staging/rehydration. Dependent
   dry runs honestly reject MissingIsolatedParentCandidates; live links are not borrowed.
4. **Safe rollback:** impact preview, restricted later-edit/dependent-row export,
   confirmation digest, reverse dependency guards, full ownership ledger, atomic
   inverse/reset and footprint proof remain absent. Production apply stays disabled.
5. **Matched actual-service parity:** independent SS capture is from full adverse `ss`;
   ready-v3/v4 are different fixtures. Their comparisons correctly fail. A matched ready
   capture and full applied adverse rehearsal are pending. Unsupported target training
   flags/status/UI projection, slot flags/stored OT and distribution surfaces remain
   explicit gaps, not reconstructed SS output. SS nonExcused is a harness projection,
   not executed browser/CSV evidence.
6. **Scheduling refinements:** complete reviewed classification,
   historical FTE/conflicts, cancellation/partial-outside cases, broader series
   qualification and approved future OT differences need further implementation/validation.
7. **Business dates/policies:** BL13 future expiry-policy and BL14 missing per-record
   training timezone remain measured/reported gaps. Historical UTC instants are not
   shifted to force matching display dates. Canonical future-month window is JANUARY 2027;
   earlier December convenience results are not future-month oracle parity evidence.
   Fresh ready-v4 now includes January shifts/duties and completed all primary jobs;
   Latest Report covers 74 rows but fails the mismatched adverse capture (33 actual target rows).
8. **Synthetic validity:** SH fit ±20%, full extractor inventory, repeat-build structural
   hashes and stable-clock generation have not passed. Ready-v3 has no January roster;
   separately named ready-v4 adds it without rewriting any earlier fixture.
   Actual target availability probes measured 1,320 suppressed user-minutes outside
   each two-hour away interval. A separately named future adverse interval correctly
   holds Scheduling NeedsAction; this is not an approved availability difference or
   an executed SS availability oracle.
9. **Independent coverage:** broader PostgreSQL concurrency, stale fencing, all mapped
   drift, retry, backfill, rollback and dry-run parity coverage remains required.

The original deleted `docs/data-migration/ss-mapping-contracts.md` stays deleted and
unstaged. No application scheduling/training/auth behavior, JC defaults, generated web
client, tenant GitOps or shared app database was changed. No push was performed.
