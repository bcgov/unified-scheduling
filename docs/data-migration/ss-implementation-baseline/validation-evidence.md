# SS Migration Validation Evidence

**Session started 2026-11-16. Initial implementation runs; acceptance is Fail / NeedsAction.**

## Planning Checks

| Check | Evidence / result |
|---|---|
| Active feature | `.specify/feature.json` points to `specs/001-data-migration-jobs` |
| Existing app branch | `Feature/SS-872-ss-migration-job`; no branch created/switched |
| Initial worktree | Pre-existing deleted `docs/data-migration/ss-mapping-contracts.md`; preserve and do not stage |
| Workspace Git ownership | Workspace specs/documentation are outside a Git repo; versioned mirror needed for requested milestone history |
| Plan hooks | `.specify` has no extensions.yml; no hooks apply |
| Setup/prerequisites | Initial RTK/Bash wrapper failed; native Git Bash via argument array passed setup-plan and check-prerequisites --require-tasks --include-tasks; existing plan preserved by setup |
| Checklist gate | requirements.md: 16 total / 16 checked / 0 unchecked, PASS; markers not edited |
| Document links/backlog | Local-link check: 0 missing paths; BL-1..6 all transferred |
| Source findings | Final worksheet through G-19/BL-6, split/cutover/report/dry-run/rollback design, production SH extract read |
| Runtime/tooling | Prior inspection: host SDK8; target needs .NET10 container. Existing `ss-synth-pg` stopped; persistence/contents not yet verified |

## Runtime Checks

Pending implementation evidence. Record exact commands, counts and artifact paths here.
Do not promote old fixture checks, code inspection or unit-test success to migration validity.

| Gate | Current result |
|---|---|
| Persistent source schema/health | Healthy isolated PostgreSQL12 source/PostgreSQL16 target; original source preserved and accepting connections |
| Production-shape fit +/-20% | Exact original SH SQL passes two independent shape builds: 4,631 numeric cells within bounds, 1,222 suppressed bounds satisfied, 53 production-withheld/noncomparable cells; all 5,906 cells accounted, zero missing/duplicate/unexpected buckets |
| Actual SS report oracle | Adverse source: 29 tests/0 skipped. Matched ready-v4 source: 33 tests/0 skipped; training/full-report/availability replay hashes match. Target parity still pending |
| MyTeam identity/auth-field persistence | Ready-v4 Completed, 41 source rows; role create/link and actual LoanOut away mapping fixed. Real login unproved |
| Training exact facts/report comparison | Matched fixture completion facts agree; Active is a derived compliance status, not a migrated attendance fact. Excused exposure/full export parity unproved |
| Scheduling intervals/links/publish parity | Ready-v4 Completed, 23 source rows/0 rejected/failed. Matched availability has 2 candidate-presence/4 conflict-response differences over 12 windows; exact two-hour calendar interval preserved |
| Full outcome coverage / accepted verdict | Matched ready-v4 Report FAIL: 74 source records covered; 57 source/33 target response rows, 39 missing/15 unexpected comparison rows. Not lost-record counts |
| Replay/conflicts/retry/backfill | PG interruption/resume/retry/backfill and merge/split/multi-entry Duty/changed-series replay pass; membership hashes, history retention and external-drift rejection exercised |
| Dry-run business rollback | PG parent/child candidate simulation, repeats and wrong/stale lineage rejection/recovery pass; applied rows/crosswalks/progress/sequences unchanged, zero leaked schemas/business audits |
| Confirmed rollback/footprint proof | Encrypted inverse journal, confirmed digest and dependency guards implemented; PG preview-confirmed inverse restores exact prior graph footprint. Broader coverage pending |
| Build/tests/EF/format | Latest SDK10 API/Runner builds pass; 103 migration + 5 Hangfire tests pass, zero skipped. EF/format/Compose checks pass. Exhaustive concurrency/inverse/release coverage remains open |

Use SS-V001..057 from the [stable validation register](../../documentation/data-migration/ss-job-notes/validation-tests.md).
SS-V040/041 are superseded, not unimplemented ownership work.
SS-V036's old app-default edit is now BL-9; migration-only explicit-false preflight remains required.

## Oracle Milestone

Legacy commit `beea0fbc91545cd87c754c47736047c9b07c1951`, not pushed.
[Harness](../../legacy/sheriff-scheduling/test-harness/report-oracle/README.md):
actual-service training/export comparison, optional shift/duty/distribution/overtime capture,
fixed windows, synthetic marker/environment guard and read-only transaction.
SDK5 build passed; `Invoke-Oracle.ps1 -Mode TestUnit`: 20 passed, zero skipped.
Unconfigured CLI/PostgreSQL cases fail closed. No ordinary application files changed.
Initial results proved harness unit behavior only. Follow-up source execution is below.

### Actual Source PostgreSQL Follow-Up

Legacy commits `e2da417` / `9a57c54`, not pushed. 29 tests passed, zero skipped, including
two PostgreSQL tests. Actual SS service captures and fresh replay matched. Synthetic database
marker/read-only transactions and current-user-only artifact ACLs verified; no CHES/JC/PDF
effects or source mutation. The source schema/data remain owned by the mock harness.

| Captured rows | Past month | Cutover +/-14 days | First future month |
|---|---:|---:|---:|
| Training API | 1,102 | 1,102 | 1,102 |
| Non-Excused projection | 1,070 | 1,070 | 1,070 |
| Shifts | 1,533 | 916 | 258 |
| Duties | 1,053 | 998 | 1,008 |
| Duty slots | 1,017 | 624 | 186 |
| Distribution conflicts | 1,772 | 1,048 | 387 |
| Overtime calculations | 1,469 | 875 | 249 |

Restricted/ignored `test-harness/report-oracle/artifacts/` contains
`source-training-v1-9a57c54.json` (6,516 rows), `source-all-v1-9a57c54.json` (22,868),
and `source-summary-v1-9a57c54.json` (counts, hashes, guards).
The Non-Excused projection is derived by the harness, **not executed UI/CSV evidence**.
[Target envelope](../../legacy/sheriff-scheduling/test-harness/report-oracle/TARGET-EXPORT.md)
requires independently executed target services and source-ID crosswalk normalization.
No target parity follows from a source-only pass.

## Initial Unified Runtime Milestone

Local commits `b589bee4` -> `e16b559b` -> `7287236` -> `21ae1f3`, not pushed.
Measured report (local-only: `2026-11-16-rehearsal.md`)
records exact commands/run IDs. Initial implementation is explicitly incomplete, not a pass.
Source `unified-ss-migration-source-1` and target `unified-ss-migration-target-1` are healthy,
unless-stopped, no host DB ports. Existing `ss-synth-pg` and anonymous volume retained.

Adverse fixture: 83 users, 547 training histories, 38,826 shifts, 32,713 duties,
26,917 slots, 3,388 manifest rows. These are **not production-shape-fit evidence**.
Adverse MyTeam dry run: 2,233 outcomes, 49 Rejected, 183 NeedsAction, zero provider-write
failures; business rows rolled back. Duplicate-region/unknown/scoped-type cases are genuine
adverse data, but missing Sheriff Administrator creation is a mapper defect, not approved
rejection. Fix and rerun required.

Ready-v2 MyTeam/Training Completed; Scheduling NeedsAction, with 12 source-row outcomes plus
one separate validation marker. It holds three ShiftEntries, three AssignmentEntries,
three staff links and seven Events (five Active/two Draft). Unchanged Training replay stable.
Actual target report/WorkingHoursService queries execute, but limited fixture/disabled LoanOut
and missing source capture **do not establish full workflow parity**.

### Second Implementation Milestone

Commits `67982bc2`, `5bd55720`, `edb9c393`, `e29fdd87`, `35dc8164`, not pushed.
Role creation bug fixed; 181/183 adverse away rows now map through current LoanOut, two
reject unresolved parent users. Corrected adverse dry run: 2,233 rows, 48 Rejected, zero
provider Failed and no blanket away placeholder; real business writes roll back.

Ready-v4 MyTeam/Training/Scheduling Completed with 41/10/23 source rows, no rejected/failed.
Actual LoanOut availability/calendar, qualified recurrence, source singleton default times,
publication preview and isolated-clone publication exercised. Selected owned UserRole update
retains target ID; later external text edit blocks the next source update without overwriting.
Latest Report covers 74 rows but fails the wrong-fixture capture and unsupported comparison
surfaces. Matched ready-v4 SS capture requested; this is coordination, not evidence of parity.

Migration test compilation is repaired: 71 migration and 5 Hangfire tests pass, zero skipped.
These are not a substitute for missing full PostgreSQL retry/backfill/rollback coverage.
Production apply remains disabled while recovery/progress implementation is unfinished.

### Durable Recovery Follow-Up

Commits `40fd1b9` and `dd86557`, not pushed. SDK10 build and 76 focused tests passed,
zero skipped. Real PostgreSQL interruption retained 14 prior batches and rolled back the
active batch; restart, below-cursor dependency retry and separate 2014 backfill succeeded.
Mapped text-only updates, new touching shift member, profile preservation and independent
target training-note drift rejection were exercised on separate probes.

Source snapshots still rescan for full group/dependency reconstruction; this is a performance
limitation, not an assertion that durable target progress is absent. Multi-root repartition,
changed series qualification/membership, dry-run parent staging and full confirmed rollback
remain incomplete.

Ready-v4 oracle handoff initially failed its exact-database guard: the separately named
restricted env file still selected `ss`. Commit `0108ca5` corrects its actual connection
property; the owner independently reparsed it with Npgsql and confirmed `ss_ready_v4`.
The original adverse config is unchanged. SDK10 build and 76 tests passed, zero skipped.
Corrected metadata is now relayed for matched capture; no matched result is recorded yet.
Actual target partial-day availability probes show 1,320 suppressed user-minutes outside each
two-hour interval. This alone **does not prove an SS difference**; actual SS availability
comparison was requested before declaring BL-15 a confirmed primary-goal blocker.

The matched source probe uses home location `900000`, destination `900001`, sheriff
`11111111-1111-4111-8111-111111111111` and away row `900000` in `ss_ready_v4`.
Source timezone is null; target fallback is `America/Vancouver`. UTC windows are
2026-11-03 08:00-10:00, 10:00-12:00 and 12:00 through 2026-11-04 08:00.
Both locations must be queried through actual SS services before classifying a difference.

### Matched Ready-v4 Source Oracle

Legacy commit `e6e674c13168dbd606f71793ec5c71c62792abb1`, not pushed.
33 tests passed, zero skipped; exact database `ss_ready_v4`, synthetic marker, read-only
transaction and private artifact ACLs verified. Fresh training/full-report/availability
replays matched; original adverse artifact hashes are unchanged.

Restricted oracle artifacts:

| File | Result |
|---|---|
| `ready-v4-source-training-v1-80e9aae.json` | 6 rows |
| `ready-v4-source-all-v1-80e9aae.json` | 57 rows; SHA-256 `DC28370841D1B444D3950E897EDEE2AA268ACC1EDAD7DFDCCDEECA2B1EDC62C7` |
| `ready-v4-availability-v1-e6e674c.json` | 12 actual service calls; SHA-256 `002C5DB06A157EB1A79413608E66C0E437FFC362C1DB194B4D8E90DF1BB88D24` |
| `ready-v4-source-summary-v1-e6e674c.json` | Counts, hashes and safety results |

Actual SS home-location probes return no conflict before/after, one Away conflict inside.
The whole-day response retains only the original 10:00-12:00 UTC interval, timezone null.
Destination strict-outside probes omit the sheriff; boundary-touch probes return the sheriff
without conflicts. Raw SS service results therefore do not support whole-day home suppression.
This is not a free-minute calculation, UI comparison or accepted target parity. Exact matched
target comparison is requested; the future `ss_ready_v4_probe` source run awaits its own
restricted connection file.

### Rollback Journal And Shape Checks

Unified commits `5ecbf206` and `d5f832e9`, not pushed. Owner reports SDK10 build,
76 focused tests (zero skipped), whitespace checks and PostgreSQL inverse/reapplication
probes passed. Encrypted rollback journal, confirmed impact digest, dependency guards,
Duty text/dependency replay and actual availability export are implemented. Current
MyTeam/Training/Scheduling loaders complete with zero pending retries. This does not prove
all rollback ownership, conflict, dependency-race or failure-atomicity cases.

SH-08 has 24 failing buckets: the fixture is not production-fit. SH-01..07/full fit,
repartition, multi-entry Duty updates, changed-series replay, dry-run lineage candidates
and matched target Report remain unfinished. Production apply stays disabled.

### Matched Cross-App Report

Unified commit `29a8ea59`, not pushed. Matched Report run
`a92f76fa-ff60-4c24-ab8e-7835fbcd1d2d` is **FAIL** and covers 74 source records.
Actual response comparison has 57 source and 33 target rows, with 39 missing and
15 unexpected comparison rows. These are response-shape/group/field discrepancies,
not equivalent to lost-record counts. Per-surface business-impact classification remains
required; the prior wrong-fixture failure is no longer the latest result.

Actual availability comparison reports two candidate-presence differences and four
conflict-response differences across 12 windows. The calendar retains the exact two-hour
interval. Fresh replay is byte-identical; no UI or free-minute parity is claimed.
The future source oracle now has its own restricted connection file with verified
`Database=ss_ready_v4_probe`, marker and 22/22 SELECT grants; its execution is pending.

SDK10 build, 76 focused tests (zero skipped), Compose/format and CRLF-aware whitespace
checks passed. See the matched report (local-only: `2026-11-16-matched-report.md`).
Repartition/multi-entry/changed-series replay, dry-run parent staging, broader integration
coverage and SH fit remain unfinished. Production apply remains disabled.

### Future Partial-Day Source Probe

The actual SS availability test and fresh replay passed on `ss_ready_v4_probe`, using
revision `e6e674c`. Restricted `probe-v4-availability-v1-e6e674c.json` contains 12 service
observations; SHA-256 `874D6B412686D940517685C0A3D4BF73F5A3D866AE11B19E55CF70D0051EB9F6`.
Exact database, synthetic marker, read-only transaction and private ACL checks passed;
all six prior artifact fingerprints are unchanged. No source code or schema changes.

| Future source window | Home response | Destination response |
|---|---|---|
| Before/after, touching interval boundary | Sheriff present, no conflicts | Sheriff present, no conflicts |
| Inside | Sheriff present, one Away conflict | Sheriff present, one Away conflict |
| Whole day | Sheriff present, one Away conflict | Sheriff present, one Away conflict |
| Strictly before/after | Sheriff present, no conflicts | Sheriff absent |

Whole-day conflicts retain exactly 2026-12-10 10:00-12:00 UTC, timezone null. This proves
raw SS interval sensitivity for the future fixture, not whole-day home suppression.
Matched future target comparison is requested. No UI, free-minute or target-parity claim.

### Graph Replay And Future Target Comparison

Unified commit `44ce6a78`, not pushed. SDK10 build and 76 focused tests passed, zero
skipped. Actual PostgreSQL merge/split, multi-entry Duty, changed-series, failure/retry,
history preservation and independent target-drift rejection cases passed. Preview-confirmed
inverse restored the exact prior footprint. Full-membership hashes, unchanged-root retention,
guarded rebinding and Duty segments are implemented. Formatting/Compose/PowerShell checks
passed; CRLF-aware whitespace checks passed after raw checks flagged line endings.

Future BL-15 comparison is **FAIL**: 12 matched actual service calls show two destination
candidate-presence differences and four missing Away conflict responses. The exact
10:00-12:00 UTC calendar interval is retained. This is measured future scheduling-service
parity loss, not lost calendar data or an executed UI result. Production acceptance is blocked;
do not alter dates or reproduce SS rules in the migration to conceal the difference.

The matched Report's 39 missing/15 unexpected rows comprise 15 paired value differences,
two merged aliases and 22 unexecuted-surface rows. Matched training completion facts agree;
`Active` is a derived compliance status. Excused exposure remains unproved. Surface/field
classification still needs full counts; unexecuted surfaces remain missing evidence, not
accepted differences.

Dry-run parent staging, SH fit, broader integration coverage, adverse rehearsal and unresolved
business parity remain open. No acceptance or production-apply authorization follows.

### Durable Dry-Run Lineage

Unified commit `5d682093`, not pushed. T065 is complete: migration-owned candidate
manifests/links support isolated simulation and rehydration with identity/version guards.
Actual PostgreSQL parent/child dry runs, stable repeats, wrong/stale lineage rejection
and recovery passed. Applied rows, crosswalks, progress and business sequences are unchanged;
zero leaked schemas or business audits. Dry Report remains **NeedsAction74 / simulation-only**,
never evidence of applied acceptance.

SDK10 Runner build, 76 focused tests (zero failed/skipped), CSharpier, Compose and PowerShell
parsing passed. Final whitespace check passed; earlier raw CRLF warnings are documented.
See dry-run evidence (local-only: `2026-11-16-dry-run-candidates.md`).

The complete difference table (local-only: `2026-11-16-matched-differences.md`)
accounts for 15 paired differences: slots 6 rows/24 field cells, shifts 6/19 and training
3/9; plus two merged aliases and 22 unexecuted-surface rows. Counts describe comparison
responses, not lost source records. Stored training facts match; derived compliance and
Excused exposure remain separate gaps. SH fit, full adverse rehearsal, broader coverage
and production parity remain open.

### Deterministic V10 Shape And Adverse Accounting

Unified commits `9814d8dd` and `4ee28f61`, not pushed. Two independent v10 builds
measure 4,631 comparable cells within +/-20%, 1,222 suppressed bounds satisfied and
53 explicitly noncomparable cells, with zero measured failures. There are 873 users;
actual parent containment/location mismatches are zero. Rebuild hashes match. Filled-source
refusal preserves canonical hashes and physical business-row versions. This supersedes
the earlier SH-08 24-failure fixture, which remains historical evidence.

Original extractor SQL verification and explicit reconciliation of the 53 noncomparable
cells remain required before marking T060 complete. The independent measurement is not
silently treated as exact-query verification.

Adverse run accounts for 2,233 exact source identities with 48 expected rejects; 11 planted
outcomes match. Another 3,377 downstream cases are **unexecuted** under dependency guards,
not passing. A separate valid-parent downstream fixture is requested; preserve the original.

SDK10 build and 76 focused tests passed, zero skipped; Python, PowerShell, Compose and
formatting checks passed. CRLF-aware staged whitespace check passed.

The 22 unexecuted matched response rows are distribution conflicts 10/3/2,
distribution sheriffs 2/1/1 and harness Non-Excused projection 1/1/1 across
past/cutover/future windows. The projection is not executed UI/CSV evidence.

### Full Valid-Parent Adverse Execution

Unified commits `f8116d6e`, `2bd48d33`, `51e19216` and `a9e9b910`, not pushed.
101,426 selected identities are exactly accounted, including 98,625 Scheduling rows.
Scheduling correctly remains **Failed**: 33 synthetic-series rejects, one failed DutySlot
and one blocked publish preview. Report acquisition is rejected rather than promoted to pass.

All 3,388 manifest rows are classified: 3,148 structural merge proofs, 155 ledger-only
matches, 30 outcome differences, two merge mismatches, 43 unverified overtime expectations
and ten superseded clone cases. Classification is not equivalent to passing expected outcomes.
Rollback footprint passed with zero dangling crosswalks. The inserted counter matches
69,959 private roots; five expired shifts have Cancelled Events.

All 19 distribution observations now have actual common-service reads, superseding the
earlier unexecuted distribution count. The harness Non-Excused projection remains distinct
from actual UI/CSV evidence. Stored training facts match in the measured fixture.

SDK10 builds, 71 migration plus five Hangfire tests (zero skipped), formatter and
script/Compose checks passed. The adverse acceptance verifier **fails**, preserving differences.
RTK whitespace wrapper exited 2; direct CRLF-aware Git check passed.
See the [acceptance inventory](../../unified-scheduling/docs/data-migration/reports/2026-11-16-acceptance-inventory.md)
and valid-parent adverse outcomes (local-only: `2026-11-16-adverse-valid-parent.md`).

Original SH extractor SQL is present at
[ss-shape-extractor.sql](../../documentation/data-migration/ss-job-notes/ss-shape-extractor.sql);
the parent verified its read-only SH-01..08 header and relayed the exact path.
Exact-query execution remains pending, not an unavailable-artifact blocker.
SH inventory reconciles to 5,906 cells (4,631 + 1,222 + 53).

Operations API/authorization, complete findings exports, broader coverage, series suitability
and capture-query scaling remain unfinished; they are implementation gaps, not evidence that
the only remaining issue is app-owned BL-15. Production stays disabled.

### Operations And V20 Final Adverse Pass

Unified commits `1d83eead`, `a33613ac`, `896bf8f6` and `d2385eba`, not pushed.
Existing-framework authorized operations, scoped controls/recovery, complete redacted
CSV/Markdown exports and rollback guards are implemented. Actual PostgreSQL operations,
series replay/drift and 100k-alias lookup regressions pass. SDK10 API/Runner builds,
103 migration plus five Hangfire tests (zero skipped), and CRLF-aware whitespace checks pass.
The only application-schema addition is a migration-owned reverse crosswalk index.

V20 accounts for 101,426 selected identities, including 98,625 Scheduling rows.
Scheduling has zero rejected and two failed outcomes. The 33 former synthetic-series rejects
now retain 20,224 actual standalone Duty roots without partial series attachment.
There are 69,959 created roots and zero dangling scheduling crosswalks. Capture performs
one full-root read plus 12,189 fresh component reads. Runtime is 4h30m50s, **not**
production-sizing acceptance.

The failed DutySlot is Dec 3 17:00-23:00 UTC: neither overlapping source shift can supply
an admitted user link because both require future-gap repair. Actual publication preview
also remains blocked. No user link is invented and no failure is waived.
Full execution/rollback assertions pass while the adverse acceptance verifier still fails.
Manifest accounting remains 3,148 structural proofs, 155 ledger-only matches,
30 differing outcomes, two merge mismatches, 43 unverified overtime expectations and
ten superseded clone cases.

See the [final v20 report](../../unified-scheduling/docs/data-migration/reports/2026-11-17-adverse-v20-final.md).
Original SH exact-query verification remains unexecuted, not missing SQL. Exhaustive
validity/security/concurrency/inverse coverage and final canonical mirror are unfinished.
BL-15 still blocks production acceptance; ordinary app behavior is unchanged.

### Exact Original SH Extractor Execution

Unified commit `d3a05fe6`, not pushed. Original workspace SQL, repo input copy and container
copy have identical SHA-256:
`70C0BB834026A117BF32F8D1FAD7A40F7BC4039BE9BEBFBEE8B8CCC023232920`.
Actual `psql -X -f` executions on `ss_shape_v10`, `ss_shape_v10_rebuild` and
`ss_adverse_valid_v18` each exit 0, report read-only/PG12.22 and have empty stderr.
No query replacement, clock substitution or fixture repair was used.

Both shape databases exactly cover 1,565 rows / 5,906 production cells with no missing,
duplicate or unexpected buckets. Full comparison files are byte-identical across builds.

| Profile | Numeric within +/-20% | Suppressed bounds satisfied | Production-withheld/noncomparable |
|---|---:|---:|---:|
| SH-01 | 2,914 | 406 | 0 |
| SH-02 | 184 | 25 | 25 |
| SH-03 | 171 | 9 | 18 |
| SH-04 | 94 | 1 | 1 |
| SH-05 | 228 | 3 | 9 |
| SH-06 | 851 | 767 | 0 |
| SH-07 | 169 | 6 | 0 |
| SH-08 | 20 | 5 | 0 |
| Total | 4,631 | 1,222 | 53 |

The 53 cells are deliberately withheld production shares/medians/percentiles, not passes
or removed denominators. Filled-source refusal/rebuild proofs remain applicable.
The adverse database deliberately fails shape comparison: 5,437 missing cells, 233 failed
bounds, 20 suppressed-indeterminate, 16 actual-withheld, 147 matching, 53 production-withheld
and 31 unexpected buckets. Comparison wrapper exits 1 separately from successful psql exit 0.
Do not describe the adverse database as production-shaped.

Restricted artifacts retain exact argv/timestamps, SQL/output/execution hashes and cell
results. Python compilation, independent manifest/hash/denominator/rebuild verification and
Git whitespace checks pass. See [original SH execution](../../unified-scheduling/docs/data-migration/reports/2026-11-17-original-sh-execution.md).
T060 is complete; production acceptance remains NO-GO for the separate business gaps.

### Closeout Resource Check

Parent ran a read-only inspect on the three explicitly named containers: `ss-synth-pg`
is running; `unified-ss-migration-source-1` and `unified-ss-migration-target-1` are running
and healthy. All three use `unless-stopped`. No container, database or volume was removed.
Production enablement is not authorized by resource readiness.
