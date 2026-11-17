# Full adverse rehearsal with valid parent lineage

Production remains disabled. This is isolated rollback-only execution, not
production acceptance. The original adverse database and all earlier failures
remain intact.

## Fixture and execution boundary

The source `ss_adverse_valid_v11` is a physical clone of the original adverse
`ss` database. All **3,388 manifest rows** and the full workload are retained:
38,826 Shift, 32,713 Duty and 26,917 DutySlot rows. It is not a reduced fixture.

Exactly five root rows were repaired in the clone to allow a genuine MyTeam
parent to complete: two ambiguous Region names, two invalid/scoped leave-code
references and one unresolved rank description. The clone records the repairs
in `synthetic.valid_parent_repairs`; the recipe is
`docker/migration/adverse-valid-v11-parent.sql`. The five original rejection
expectations and five dependent leave expectations are superseded **only in
this clone**, not passed. The original adverse fixture retains those failures.

Children acquire their parent through the real job coordinator. There are no
fabricated Completed run rows or applied crosswalks. Training and Scheduling
may run in parallel; Report must reject acquisition while Scheduling runs.

## Preserved attempts

| Target / attempt | Actual result | Coverage interpretation |
|---|---|---|
| v11 first MyTeam | Failed; 2,233 scanned /254 rejected | A proposed repair collided with an existing retained code. Failed run retained |
| v11 corrected MyTeam | Completed; 2,233 scanned /0 rejected /0 failed | Genuine parent, not a seeded lineage |
| v11 Training | Completed; 568 scanned /0 rejected /0 failed | Actual downstream execution |
| v11 Scheduling | Abandoned through actual stale recovery after controlled stop | No manifest acceptance credit; ordinary/applied/progress/business sequence/audit footprint unchanged |
| v12 MyTeam /Training | Completed; 2,233 /568 scanned, no rejected/failed rows | Genuine lineage; full source retained |
| v12 Scheduling | Abandoned through actual stale recovery after controlled stop | No manifest acceptance credit; physical rollback verified |
| v12 Report while Scheduling Running | Acquisition rejected, no Report run inserted, footprint unchanged | Actual PostgreSQL dependency-guard regression passed |

The scale fixes do not change business mappings: bounded EF tracking inside
the outer rollback-only transaction, append-only target counters, indexed
source associations, and explicit flushing of pending membership mutations
before detaching. Database locks/savepoints and full-group atomicity remain.
Neither bootstrap allowlists nor dry-run overlays authorize applied execution.

## Final v13 accounting

The v13 run used the same untrimmed source, a new isolated target, a genuine
Completed MyTeam parent, and actual Training/Scheduling child execution.
All **101,426 selected business identities** have exactly one durable source
outcome, with exact identity-set equality independently checked for every
selected entity. Expected manifest labels were not substituted for outcomes.

| Job | Actual status | Scanned | Rejected | Failed | Skipped |
|---|---|---:|---:|---:|---:|
| MyTeam | Completed |2,233|0|0|110|
| Training | Completed |568|0|0|31|
| Scheduling | **Failed** |98,625|33|2|366|
| Report | Acquisition rejected |0|—|—|—|

Run IDs: MyTeam `0df4ed97-c140-4056-b154-10aa36c7f482`;
Training `a65da490-85c0-48d9-8728-72ec42151981`;
Scheduling `c32d306f-a6f2-40eb-8fcd-49d1c01376ea`.
Both children reference that actual Completed MyTeam run. No Report run was
inserted, either during Scheduling or after its failure.

Scheduling ran from `2026-11-17T01:03:15.471485Z` to
`2026-11-17T06:27:07.005879Z` (**5h 23m 52s**). This is not a production
performance acceptance. Initial component capture still rereads the full
crosswalk for disconnected components: 12,189 component proofs were written
privately, and a runtime sample already showed 368 GB received. This residual
query-scaling issue needs optimization/profiling before production sizing.

### Exact source identity accounting

| Job | Entity counts, each independently matched to source IDs |
|---|---|
| MyTeam | Region6; Location15; Role3; User83; UserRole110; LookupSortOrder109; acting8; away183; leave1,699; Type7 positions8; Type5 leave types9 |
| Training | Type6 catalogue21; history547; standard requirements0 |
| Scheduling | Assignment67; Duty32,713; DutySlot26,917; Shift38,826; rooms18; classifications84 |

Scheduling physical-source outcomes: **93,336 Migrated; 3,128 Merged;
1,785 MigratedWithDifference; 366 Excluded; 5 DroppedBadData;
4 NeedsAction; 1 Failed =98,625**. The 33 rejected outcomes are synthetic
AssignmentSeries `InconsistentSeriesMember`, not 33 missing source records.
Series suitability/fallback remains unresolved; standalone duties remain,
and no fake recurrence occurrences were generated. Nine series were linked,
16 lacked sufficient occurrences, and nine had missing RRULE occurrences.

The two failed outcomes are one physical DutySlot `MaterialMissingShiftUserLink`
and one actual `SchedulingReadValidation/ActualPublishPreviewBlocked`.
Actual service reads executed 45 location/window groups: 44 succeeded and one
futuremonth publish preview blocked. Four source shifts remain
`FutureGapRequiresSourceRepair`. Another 41 future Away diagnostics remain
`SsComparisonPendingFutureTargetOutsideIntervalSuppression`; these are not
41 independently source-matched BL15 proofs or approved business differences.

The independent evidence check requires exact selected-source identity sets,
one durable outcome per selected identity, matching scanned totals, actual
member crosswalk multiplicity for merge claims, real cancelled Event roots for
successfully transferred expired shifts, and no dangling scheduling crosswalks.
The private inserted counter is checked against distinct actual created target
roots before rollback. All private graph receipts stay ignored/restricted.

**Actual regression results:** 69,959 distinct private created roots exactly
matched the inserted counter; 37,258 Shift roots and 32,609 Duty-entry roots
existed; zero dangling scheduling crosswalks. All five successfully transferred
expired source shifts pointed to real cancelled Events. The ordinary business,
applied crosswalk/mutation, progress, sequence and business-audit physical
footprint stayed unchanged; no transaction-private schema escaped rollback.

### Every manifest row, with exceptions retained

The evidence verifier exits **1**, deliberately, for actual expectation
differences. Successful row accounting/footprint checks do not change that verdict.

| Case / source surface | Rows | Actual result / interpretation |
|---|---:|---|
| G12E same-sheriff DutySlot |60|Migrated; real shared member crosswalk proves structural merge |
| G12E flag-only beside staffed DutySlot |20|MigratedWithDifference /FlagOnlySegmentBesideStaffDropped, not literal expected DroppedBadData |
| G13C no-shift DutySlot |5|DroppedBadData /NoOverlappingShiftUserLinkDropped |
| G16 historical conflict DutySlot |5|Migrated, not expected MigratedWithDifference; case-specific conflict/finding acceptance unverified |
| G17 identical Shift |40|Merged; structural shared-target proof |
| G17 touching Shift |3,048|Merged; structural shared-target proof, not expired-shift tests |
| G17 historical gap Shift |30|MigratedWithDifference /HistoricalGapBlocksSeparate |
| G17 future gap Shift |4|NeedsAction /FutureGapRequiresSourceRepair |
| G19 stored-excess SheriffDay |40|Logical overtime expectation, not independently verified |
| G19 cross-location-overlap SheriffDay |3|Logical overtime expectation, not independently verified |
| G3 Assignment |2|Excluded /ApprovedTestOrLeaveWaitlist |
| G3 Duty |104|Excluded /ApprovedTestOrLeaveWaitlist |
| G8 same-type Assignment |2|MigratedWithDifference /StatsAssumptionC1S3; two targets, expected merge not observed; fixture detail below |
| G8 cross-type Assignment |2|MigratedWithDifference /StatsAssumptionC1S3 rather than Migrated; fixture does not establish a full-name collision |
| QL4 LookupCode |1|Original rejection superseded by parent repair; actual Migrated |
| QL4 dependent leave |2|Superseded only in clone; actual Migrated |
| QL6 LookupCode |1|Original rejection superseded by parent repair; actual Migrated |
| QL6 dependent leave |3|Superseded only in clone; actual Migrated |
| QT6C categories /LookupCode |9|Migrated; literal ledger result only |
| Ambiguous Region |2|Original rejection superseded by parent repair; actual Migrated |
| SP04 future-conflict training |3|MigratedWithDifference /TargetTrainingTimezoneUnavailable, not expected NeedsAction; no per-case future-conflict acceptance claim |
| T7 description-match User |1|Migrated |
| T7 unknown-rank User |1|Original rejection superseded by parent repair; actual Migrated |
| **Total** |**3,388**|No manifest rows omitted |

Classification totals: **3,148 structurally verified merge members; 155 exact
ledger labels only; 30 differing outcome labels; 2 structural merge mismatches;
43 unverified logical overtime expectations; 5 superseded root repairs;
5 superseded dependent leaves =3,388**. “Exact ledger” is not independent
business-parity proof.

G8 exception inspection used aggregate comparisons, not exported text. The
same-type pair has one location/type but **two**
`upper(trim(proposed display name))` values. Trimming the source-name component
*before* adding parentheses would produce one, but that is not the supplied
canonical full-display-name rule. Do not silently change normalization or call
the expected merge a pass. The cross-type pair also has two distinct proposed
full names, so it does not exercise same-name type suffixing. These fixture
expectations need reconciliation or additional correctly constructed cases.

Forty-three `SheriffDay` manifest rows are logical overtime expectations, not
source-table identities. Row transfer outcomes alone cannot prove their
business result. These remain unverified without an independent matched actual
service comparison. Superseded root/dependent cases and logical expectations
must remain separate from literal outcome matches and structural merge proof.

### Retained evidence

Ignored restricted files (never committed):

- `adverse-valid-v13-evidence.json`, SHA-256
  `3233B3EF6CD20134CC6807202DAC5B00C26A4B0B619585CFF7D4EC4FC7B9143F`.
- `adverse-v11-graph-c32d306f-a6f2-40eb-8fcd-49d1c01376ea.json`, SHA-256
  `589EBE58132A802782B61CEB47CC7A383F567F0CCB9C655F23D61CA9B202B0D2`.
  The legacy filename says v11; its independently checked metadata identifies
  the exact v13 target and v11 source clone.

Reproducible checks:
`Invoke-AdverseValidV11.ps1 -Version 13 -Step ReportWhileRunning`,
`-Step Report`, `-Step Verify`, and `adverse_valid_v11_evidence.py`.
Report CLI exit1 is expected dependency rejection; the Python exit1 is the
real retained adverse expectation failure, not a harness pass.

## Build and verification record

| Check | Result |
|---|---|
| SDK10 Runner build, private `/tmp/ss-final-validity-runner` output | PASS,26 warnings/0 errors |
| SDK10 test build, private `/tmp/ss-final-validity-tests` output | PASS,46 warnings/0 errors |
| Actual xUnit `-namespace Unified.Tests.DataMigration` |71 passed/0 failed/0 skipped |
| Actual xUnit `-namespace Unified.Tests.Hangfire` |5 passed/0 failed/0 skipped |
| CSharpier check, five changed C# files | PASS |
| Python AST, PowerShell AST, v11/v12/v13 Compose validation | PASS |
| Actual PostgreSQL lineage guards /physical rollback /private counter /cancelled roots | PASS, not business parity |
| Exact selected identity sets and all manifest rows accounted | PASS accounting; adverse verifier exit1 retains real differences |
| Scheduling business verdict /Report acceptance | **Failed /blocked** |
| Required `rtk git diff --check` | Wrapper exit2 without diagnostic output on this Windows checkout |
| `rtk proxy git -c core.whitespace=cr-at-eol diff --check` | PASS; no Git configuration changed |

Builds used SDK10 containers, not host SDK8. Private build outputs did not
overwrite assemblies used by the active full-source worker. Operational PG
assertions are additional real-DML checks, not invented new xUnit test counts.

## Acceptance limits

Successful rollback/accounting is not a business-parity pass. Any differing
manifest outcome, future conflict, unverified overtime case or validator
failure remains explicit. BL15 independently remains an app-owned primary
scheduling blocker; migration events are not widened or cloned to hide it.

Subsequent [operations API/permissions/exports](2026-11-17-operations-api.md) and
[series/capture fixes](2026-11-17-series-capture.md) are implemented; they do not
rewrite this retained failed rehearsal. See the
[delivery inventory](2026-11-16-acceptance-inventory.md) for independent release
coverage. See [actual distribution reads](2026-11-16-actual-distribution-reads.md)
for the completed common-service projections and their narrower evidence scope.
