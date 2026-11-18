# SS-V001..057 validation ledger

**As of 2026-11-18.** This ledger classifies retained evidence; it does not claim
that every check passed or authorize production writes.

**Status key:** Pass = evidence covers the stated check in the recorded scope;
Fail = the check produced an unacceptable result; Partial = some evidence exists,
but required coverage remains; Known blocker = measured failure or external
dependency; Pending = no sufficient evidence; Deferred = owner postponed the
check; Accepted difference = owner accepted the stated variance only;
Superseded = replaced by an approved rule. Production sign-off is listed
separately because it is not an SS-V test.

Sources: [stable validation register](../../../../documentation/data-migration/ss-job-notes/validation-tests.md),
[validation evidence](validation-evidence.md),
[acceptance inventory](../reports/2026-11-16-acceptance-inventory.md),
[v20 final report](../reports/2026-11-17-adverse-v20-final.md).

| ID | Status | Evidence / remaining work |
|---|---|---|
| SS-V001 | Pending | No isolated business-key result in retained evidence. |
| SS-V002 | Partial | Adverse invalid/ambiguous rows were exercised; full key-case matrix is not recorded. [Inventory] |
| SS-V003 | Partial | Replay and external-drift guards passed selected PostgreSQL cases; complete timestamp matrix remains open. [Inventory] |
| SS-V004 | Partial | Actual target inserts completed; every required/invalid-field branch is not separately evidenced. [V20] |
| SS-V005 | Partial | Zero dangling scheduling crosswalks in v20; all reference and cycle cases are not separately evidenced. [V20] |
| SS-V006 | Pending | Region Code omission has no isolated evidence. |
| SS-V007 | Partial | Stable replay/crosswalk cases passed; complete missing-target and changed-identity matrix remains open. [Inventory] |
| SS-V008 | Pending | No isolated CourtRoom identity result. |
| SS-V009 | Pending | No isolated null/unresolved location-scope result. |
| SS-V010 | Pending | No isolated Type 0 source/target ID crosswalk result. |
| SS-V011 | Pending | No complete Type 0 match/insert/skip and JC matrix. T051 remains open. |
| SS-V012 | Pending | No isolated existing-CourtRoom preservation result. |
| SS-V013 | Pending | No isolated Type 0 link-only rollback result. |
| SS-V014 | Accepted difference | Two G8 manifest merge expectations are accepted as fixture mismatches: source canonical display names differ. Do not change mapping rules to force a merge. [V20] |
| SS-V015 | Partial | Leave mapping is implemented; BL-8 remains. Code review confirms the edit form filters by the leave date and clears a selected type when it is inactive. |
| SS-V016 | Partial | Training facts and target execution were checked; full course/category/field contract matrix remains open. [Inventory] |
| SS-V017 | Partial | Rank and acting-position paths are implemented; approved matching, drift and rollback matrix is not recorded. [Inventory] |
| SS-V018 | Pending | Closed sort-order rule has no isolated acceptance evidence. |
| SS-V019 | Pending | No complete classification/destination assertion ledger. |
| SS-V020 | Partial | 2,233 My Team identities ran with zero rejected/failed; exact identity/auth matrix and real IDIR login remain open (T073). [V20] |
| SS-V021 | Partial | 568 Training identities ran with zero rejected/failed and stored facts matched; Excused and full compliance/report parity remain open. [V20] |
| SS-V022 | Fail / known blockers | Scheduling had one failed DutySlot because its source shifts need repair (T072); workflow parity is also blocked by BL-15. No rows were invented. [V20] |
| SS-V023 | Partial | Read-only source projections and exact original SH SQL execution are evidenced; full source/job isolation matrix is not. [Evidence] |
| SS-V024 | Partial | Focused PostgreSQL and migration tests pass; the full constraint/active-run matrix is not recorded. [Inventory] |
| SS-V025 | Partial | Batch rollback/atomicity cases passed in focused runs; full injected-failure matrix remains open. [Inventory] |
| SS-V026 | Partial | Resume, retry and cutoff backfill probes passed; 2020 boundary and exhaustive cursor matrix remain open. The 2020 default change itself was not retested. [Evidence] |
| SS-V027 | Partial | Selected retry/rejection cases passed; full watermark and dependency-blocking matrix remains open. [Inventory] |
| SS-V028 | Partial | Selected target drift and mapped-text update cases passed; full concurrent-edit matrix remains open. [Inventory] |
| SS-V029 | Pass | Actual dry-run lineage, repeat, stale/wrong-parent rejection and no-business-footprint checks passed. [Evidence] |
| SS-V030 | Partial | Confirmed inverse and selected controls passed; pause/abort/dependency race matrix remains open (T034). [Inventory] |
| SS-V031 | Partial | T047 exposure scan found no data leak. T050 endpoint review fixed disabled-flag routes returning 500 and added JSON-only unsafe requests; broader cookie-CSRF and Hangfire dashboard concerns remain BL-18/19. |
| SS-V032 | Partial | SDK10 builds, 103 migration and 5 Hangfire tests pass; exhaustive release suite and separate acceptance gates remain open. [V20] |
| SS-V033 | Pending | No isolated operational null-key region result. |
| SS-V034 | Pending | No isolated agency-key mapping result. |
| SS-V035 | Pending | No isolated SS2 seed-field preservation result. |
| SS-V036 | Known blocker | T051 explicit-false environment preflight still needs its required validation before write enablement. |
| SS-V037 | Pending | No isolated missing-region insert result. |
| SS-V038 | Pending | JC/migration ordering matrix has not been completed. |
| SS-V039 | Pending | Per-type operational lookup preservation matrix has not been completed. |
| SS-V040 | Superseded | Replaced by config-based ownership in SS-V036; do not execute the old persisted-owner design. |
| SS-V041 | Superseded | Replaced by config-based ownership in SS-V036; no backfill/report-before-backfill work applies. |
| SS-V042 | Pending | Approved zero/positive AdvanceNotice behavior lacks a complete isolated test result. |
| SS-V043 | Partial | Stored training facts match in the measured fixture; all copy-only expiry and later edit cases are not evidenced. [Inventory] |
| SS-V044 | Pending | Type 6 LocationId/SubCode rejection matrix is not recorded. |
| SS-V045 | Pending | Type 6 order allocation/concurrency matrix is not recorded. |
| SS-V046 | Pending | Deployed source EffectiveDate schema/timezone semantics remain unverified. |
| SS-V047 | Pending | Run-start timestamp fallback matrix is not recorded. |
| SS-V048 | Known blocker | Required Type 0 ownership/replay validation remains under T051. |
| SS-V049 | Partial | Role/user migration ran; all role pairing, permissions, ID collision and replay cases are not separately evidenced. [Inventory] |
| SS-V050 | Known blocker | Future publish preview is blocked pending Surrey source repair (T072); BL-15 also blocks scheduling-service parity. User accepts the blocked status, not a pass. [V20] |
| SS-V051 | Deferred | Overtime parity is deferred by owner decision; 43 logical expectations lack independent matched-service proof (T063). [V20] |
| SS-V052 | Partial | v20 proved 20,224 series-group Duty roots remain standalone and detached; direct future series insert and all cutover boundary cases remain open. [V20] |
| SS-V053 | Fail | Strict matched Report verdict is FAIL; differences are retained, not waived. [Inventory] |
| SS-V054 | Pass | Actual dry-run mode and rollback-only business footprint checks passed. [Evidence] |
| SS-V055 | Partial | Confirmed inverse restored the tested prior graph; broader edit/dependency/photo and forced-failure cases remain open. [Inventory] |
| SS-V056 | Pass | Two independent production-shaped builds passed exact SH-01..08 bounds; all 5,906 cells accounted for. The adverse fixture correctly fails shape fit. [Evidence] |
| SS-V057 | Fail / BL-15 blocker | Matched Report has 39 missing and 15 unexpected response rows; BL-15 is a measured scheduling-service blocker. These are comparison rows, not lost-record totals. Training compliance approval is still production-only (T070). [Inventory] |

## Separate production gate

**T070 — Production only:** training compliance sign-off against production data.
It is not a pass/fail SS-V ID and cannot be completed in this pre-production ledger.

## T062 owner decisions, 2026-11-18

1. Accepted the two G8 merge expectation mismatches as fixture expectation errors;
   no mapping change or forced merge.
2. Accepted the future publish preview as a known blocker until T072 repairs the
   Surrey source shifts. This is not a pass.
3. Code review confirms BL-8: both the My Team leave form and calendar edit form
   supply date-active leave types; `LeaveForm` clears a saved selection if it is
   not in that filtered list. Existing leave whose type expired for its date can
   lose its type on edit. No Unified code was changed. Keep T062 open for this gap
   and remaining coverage.

T048 is complete as a reporting task: every stable SS-V ID now has a recorded
status and evidence/next step. Pending, partial, blocked, failed, deferred, and
superseded checks remain open; this ledger does not make overall migration
acceptance green.
