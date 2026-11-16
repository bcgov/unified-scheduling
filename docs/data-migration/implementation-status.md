# SS implementation status — 2026-11-16

**Full migration acceptance remains incomplete. Guarded graph replay and durable
dry-run parent simulation are verified; this is NOT a production or migration
acceptance pass.**

Planning mirror: [ss-implementation-baseline](ss-implementation-baseline/README.md).
Workspace artifacts remain canonical. Prior milestones: `b589bee4`, `e16b559b`,
`7287236`, `21ae1f3`; primary follow-ups `67982bc2`, `5bd55720`, runner validation
`edb9c393`, singleton defaults `e29fdd87`. See the [rehearsal record](reports/2026-11-16-rehearsal.md)
for retained failures, run IDs and application-service evidence.
Durable recovery and mapped-update follow-up:
[2026-11-16 recovery evidence](reports/2026-11-16-recovery.md).
Journaled inverse executions: [rollback evidence](reports/2026-11-16-rollback.md).
Latest comment/dependency replay, chained inverse and exact-window actual guard results:
[Duty and availability evidence](reports/2026-11-16-duty-availability.md).
Original80-sheriff SH-08 census remains FAIL24/26:
[historical shape failure buckets](reports/2026-11-16-sh08-fit.md).
New isolated v10 source/rebuild independently fit all4631 non-suppressed SH01–08 cells
within ±20%,1222 suppressed bounds and53 explicit noncomparables, zero failed cells:
[complete actual shape/inventory/proof](reports/2026-11-16-shape-profile.md).
Original external extractor SQL execution remains unverified, not waived.
Preserved adverse-chain attempt: [actual rejects and blocked downstream cases](reports/2026-11-16-adverse-v7.md).
Latest bounded milestone: [matched Report and actual response gaps](reports/2026-11-16-matched-report.md).
Subsequent bounded implementation: [guarded graph replay, history/inverse proof and
matched future availability](reports/2026-11-16-graph-replay.md).
Current bounded implementation: [real-DML dry-run parent candidates and PG proof](reports/2026-11-16-dry-run-candidates.md).
Complete parent-document table: [matched surface/field differences and BL15 decision](reports/2026-11-16-matched-differences.md).
Exact ready-v4 source captures are imported and verified; fixture coordination is no
longer the Report blocker. Final matched Report is Failed, not accepted.

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
  services executed successfully. Target availability is day-based; matched future
  calls now prove a primary scheduling API parity gap (BL15), not an approved
  `MigratedWithDifference` or assertion of hour-for-hour availability.
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
  definitions/shifts/duties and qualified series:
  require changed mapped source hash, job ownership, unchanged target mapped hash
  and original root/event PostgreSQL row-version proof under row locks. Linked-only
  rows never update. SHA-256 covers mapped text/SSO without retaining/emitting raw values;
  photo bytes are excluded and additionally guarded by source stamps/target versions.
  Training profile and initial assignment-series composition refresh original owner
  concurrency receipts only after verifying unchanged owner-mapped fields.
  Actual text-only updates, new touching member, profile preservation and independent
  target training-note drift rejection ran on the separate probe.
- Full source membership merge/split/repartition and multi-entry Duty replay now
  execute atomically with original ownership/hash/version guards. Exact writer
  proposals retain unchanged physical roots; changed dependencies rebind stable
  primary crosswalks to generated targets. Actual PG cases cover definition/shift
  split+merge, second Duty segment add/change/expiry, series fallback/requalification,
  actual occurrence add/weekday removal, unchanged replay, injected40001 rollback/
  retry and target drift rejection. No fake occurrences or app behavior changes.
  Separate history PG proof preserves backfill under later cutoff and replays known
  older members without admitting new older rows. Preview-bound inverse restored
  the exact prior graph footprint (78 journaled rows, zero extra dependents).
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
- All four dry jobs execute real DML/reconciliation in a transaction-private cloned
  baseline, then roll back every business row/audit/sequence. MyTeam publishes only
  separate durable simulation candidate identities/hashes; dependent simulations
  rehydrate/verify them using actual writers without applied crosswalk substitution.
  PG cases prove stable repeats, wrong/stale lineage rejection, untouched applied
  sentinel/crosswalk/progress and no schema/business-audit leak. Dry Report explicitly
  remains simulation-only NeedsAction, never applied lineage or publication.
- Dedicated runner audit configuration excludes private source strings/SSO/photo values
  while retaining Unified audit identity/action. Earlier isolated synthetic audit copies
  were redacted without deleting business rows, run evidence or audit identities.
- **71 migration tests and 5 dependent Hangfire tests pass, zero failures/skips.**
  These are foundation/preflight tests, not comprehensive PostgreSQL integration coverage.
  Milestones additionally executed real PG topology/retention/failure/history/
  confirmed-inverse and dry-parent lineage/footprint assertions; these are not counted
  as new xUnit coverage.

## Apply safety

**Non-dry-run execution is fail-closed outside exact isolated synthetic database names
and an explicit `AllowUnrecoverableSyntheticApply=true` acknowledgment.** Journaled
preview/confirmed atomic inverse is now exercised on a new isolated target; production
coverage/activation is not accepted. Do not enable real-environment apply. Publication has a separate exact
clone-name/confirmation guard. No whole-database restore is implemented or attempted.

## Remaining acceptance blockers

1. **Mapped-update coverage:** required multi-root repartition, multi-entry duties
   and changed-series replay now execute and passed actual PG cases. Broader
   independent coverage remains; missing original version proofs are never blessed.
   The v4/v5 independent target edits and rejected source changes remain preserved.
2. **Source incremental optimization:** durable retry/backfill/checkpoints and bounded
   atomic target commits are implemented/exercised, but snapshots still rescan all
   source rows for group/identity/dependency reconstruction. Broader concurrency,
   checkpoint and recovery integration coverage remains required.
3. **Dry-run scope:** T065 isolated durable parent candidate staging/rehydration is
   implemented and exercised on PostgreSQL. Real writers operate against a private
   baseline with independent crosswalks; applied records remain untouched. Broader
   overlapping applied-ownership/update preview parity is not established by this
   fresh-namespace simulation. Code/input/baseline changes require fresh MyTeam staging.
4. **Rollback acceptance:** encrypted immutable journal, restricted impact export,
   run-bound digest, dependency guards, atomic inverse/reset and footprint proof now
   execute on PostgreSQL, including later dependents and injected inverse failure.
   Report reversal/broader adverse/concurrency cases remain; prior nonnull private-photo
   overwrite cannot restore old bytes under Users-only photo retention. Old unjournaled
   runs remain blocked. Production apply stays disabled.
5. **Matched actual-service parity:** ready-v4 matched Report
   `a92f76fa-ff60-4c24-ab8e-7835fbcd1d2d` covers74 rows but Failed: 57 source/33 target,
   18 exact/39 missing/15 unexpected strict response rows. All six duties match; the
   two-to-one historical shift merge preserves its complete interval union. Training
   Excused exposure, presentation/derived compliance status, four slot flags and
   three shift fields differ or are unexposed. All15 unexpected strict rows pair to
   source identities; not unexpected records. Stored training completion facts match;
   true-Excused policy, broad compliance and UI/date display parity are unproved.
   Nineteen distribution rows have no executed equivalent target full-distribution adapter,
   not proof of nineteen lost business records. Three source harness-projection rows
   are not executed UI evidence on either side. Full adverse target chain was attempted:
   MyTeam Failed48 rejects with exact2233-row identity/outcome accounting,11 planted
   expected outcomes and3377 downstream cases unexecuted under the correct parent gate.
   Full downstream adverse acceptance remains pending, not a passed zero-row chain.
6. **Scheduling refinements:** complete reviewed classification,
   historical FTE/conflicts, cancellation/partial-outside cases, broader series
   qualification and approved future OT differences need further implementation/validation.
7. **Business dates/policies:** BL13 future expiry-policy and BL14 missing per-record
   training timezone remain measured/reported gaps. Historical UTC instants are not
   shifted to force matching display dates. Canonical future-month window is JANUARY 2027;
   earlier December convenience results are not future-month oracle parity evidence.
   Fresh ready-v4 now includes January shifts/duties and completed all primary jobs;
   Latest Report covers 74 rows and now fails the matched ready-v4 capture; three actual
   training end instants/expiry values match, but broad BL-14 date-display parity is unproved.
8. **Synthetic validity:** new persistent v10/source rebuild passed independent local
   SQL SH01–08 fit for all5906 enumerated cells (4631 comparable,1222 suppressed
   bounds,53 noncomparables), exact22-table inventory, stable-clock independent
   generation, equal table/sequence hashes and filled-DB refusal/footprint checks.
   Source parent containment/location checks are zero; earlier failed fixtures remain.
   Original external SH/EX/AG/AD/SP/OD extractor SQL is absent from supplied repo-local
   evidence and has not run; exact extractor acceptance remains open. This source
   fit is not target business parity or a production activation. Ready-v3 has no January roster;
   separately named ready-v4 adds it without rewriting any earlier fixture.
   Actual target availability probes measured 1,320 suppressed user-minutes outside
   each two-hour away interval. A separately named future adverse interval correctly
   holds Scheduling NeedsAction. Subsequent actual SS comparison is complete. Target-only
   suppression does not quantify SS workflow loss. Actual matched SS/target read calls now
   show two destination candidate-presence differences and four conflict-response differences
   across twelve windows. Target calendar preserves the exact two-hour home event, but its
   proposed-shift conflict engine returns no Away conflict; source returns four instances.
   Date-only borrowed-user discovery and day-based assignment guards are distinct response
   planes, not executed UI/free-minute/save evidence. Future source capture and matched
   actual target comparison now executed on ss_ready_v4_probe: same12 calls, two
   destination presence differences and four missing Away conflict responses. Exact
   future calendar10–12UTC is preserved; future scheduling acceptance remains Fail/
   NeedsAction: a primary blocker requiring a Scheduling/LoanOut decision rather than
   automatic waiver. Proposed application work is interval-sensitive discovery,
   LoanOut conflict participation and assignment-guard alignment; no migration workaround
   or out-of-scope application fix is implemented.
9. **Independent coverage:** broader PostgreSQL concurrency, stale fencing, all mapped
   drift, retry, backfill, rollback and dry-run parity coverage remains required.

The original deleted `docs/data-migration/ss-mapping-contracts.md` stays deleted and
unstaged. No application scheduling/training/auth behavior, JC defaults, generated web
client, tenant GitOps or shared app database was changed. No push was performed.
