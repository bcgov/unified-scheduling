# Final bounded implementation evidence: valid-parent v20

**NO-GO. Production disabled. Complete execution/accounting is not business acceptance.**

Source `ss_adverse_valid_v18` is a PostgreSQL clone of valid-parent v11 with paging
indexes only; no business rows/manifest expectations changed. Target
`unified_ss_rehearsal_adverse_valid_v20` is new and isolated. BatchSize10;
real rollback-only EF DML and actual application validators.

## Completed actual executions

| Job | Stable run | Result | Selected identities |
|---|---|---|---:|
| MyTeam | `0c285a9b-8444-4525-9637-6fb654bf8d73` | Completed; zero rejected/failed | 2,233 |
| Training | `44c68f58-c4f0-4f5e-8de4-4296698edbb8` | Completed; same genuine parent, zero rejected/failed | 568 |
| Scheduling | `84955196-f62b-4f1c-a1ab-2a88c911c76a` | **Failed; zero rejected, two failed** | 98,625 |
| Report | No run acquired | Correctly refuses failed child lineage | Not counted |
| **Total** | | Exact unique source-identity/outcome sets for every selected entity | **101,426** |

Scheduling started `2026-11-17T10:02:48.530689Z`, ended
`2026-11-17T14:33:38.352535Z`: **16,249.821846s /4h30m50s**.
Earlier v13 was5h23m52s. This is roughly16.4% shorter, not a production benchmark:
batch size, source paging indexes, capture selection and reverse target indexing
all changed. Significant source/group/row-DML performance work remains; do not
attribute the elapsed difference solely to the index.

## Series bug fixed; primary failures retained

| AssignmentSeries outcome | Groups |
|---|---:|
| Excluded /StandaloneInconsistentMember | 33 |
| Excluded /StandaloneInsufficientPatternOccurrences | 16 |
| Excluded /StandaloneMissingRruleOccurrence | 9 |
| MigratedWithDifference /G10DraftSeriesCutoverActiveStandalone | 9 |

The 33 exclusions exactly match the retained v13 rejected group identities.
Physical graph proof checked **20,224 distinct real Duty roots** under those
groups: every mapped entry and Event has null series membership; no unqualified
series crosswalk or dangling/partial series attachment survived. This is not
merely a relabeled outcome. Actual small PG replay also covers becoming uniform,
requalification, reverting to mixed capacity, closed-slot rebind and whole-graph
target-drift rejection: [v17 cases](2026-11-17-series-capture.md).

The two remaining failures are unchanged, not softened:

1. **One DutySlot /MaterialMissingShiftUserLink:** source user exists; interval
   **2026-12-03 17–23 UTC**, America/Vancouver. Both overlapping source shifts are
   held for future gaps (`FutureGapRequiresSourceRepair`), so no admitted target
   shift/user link exists. No invented shift or gap fill is authorized.
2. **One SchedulingReadValidation /ActualPublishPreviewBlocked:** actual
   publication preview remains blocked. It is not a loader, exporter or skipped
   validator success.

BL15 remains a separate app-owned primary scheduling blocker: two destination
candidate-presence differences and four missing Away conflict responses over
12 actual same-case calls. No application fix, widened event, post-filter or loss
waiver was applied.

## Capture, ownership and rollback proofs

- **One** full root-index read, **12,189** exact fresh per-component root reads.
  Existing target hash/version/dependent guards still ran inside each component.
- **69,959** distinct private created roots exactly matched inserted counter.
- **37,258** Shift roots, **32,609** Duty-entry roots; **zero** dangling scheduling
  crosswalks. All five transferred expired-shift cases have actual cancelled Events.
- Ordinary business, applied crosswalk/progress, business identity sequence and
  business audit physical footprint unchanged after every child rollback.
- No private schema/business audit escaped. Failed-lineage Report acquisition
  refused; earlier genuine Running-child guards remain recorded.
- [Actual100k reverse lookup proof](2026-11-17-target-lookup-index.md):
  ten aliases selected through the new nonunique index; all probes rolled back.

v16/v18 interrupted profiling attempts remain retained and **not** counted as
full coverage. Their completeness assertions deliberately fail; their controlled
abort/physical rollback results are separate.

## Every manifest row classified; expectations not waived

| Manifest classification | Rows |
|---|---:|
| Structural merge membership verified | 3,148 |
| Exact ledger outcome only, not independent business parity | 155 |
| Different outcome needs review | 30 |
| Structural merge expectation mismatch | 2 |
| Logical overtime expectation not independently verified | 43 |
| Superseded root repair, original adverse case retained | 5 |
| Superseded dependent of repaired parent | 5 |
| **Total** | **3,388** |

All group details remain unchanged from the [full v13 table](2026-11-16-adverse-valid-parent.md).
The30 differences are20 flag-only slots,5 partial-outside slots,2 G8 cross-type
definitions and3 future-conflict training rows. The2 G8 expected merge members
have two distinct canonical full display names; changing the normalization to
make the fixture pass is not authorized. The43 SheriffDay expectations are logical
OT cases, not physical source rows or independently matched actual-service results.
None becomes Pass because accounting finished.

The verifier intentionally exits1 after writing complete immutable evidence.
An initial v20 accounting attempt still checked the literal v13 target name;
it failed before writing evidence. Corrected exact source/target pair guards
preserve all integrity and expectation assertions; the final verifier still fails
on the actual unwaived expectations.

## Restricted receipts / checks

Ignored restricted files, never committed:

- `adverse-valid-v20-evidence.json`, SHA256
  `EFE669191D91F867C798C32F613FBDBB69F97097DFB461C84A6BBC11780723EB`.
- `adverse-v11-graph-84955196-f62b-4f1c-a1ab-2a88c911c76a.json`, SHA256
  `C7A21A48635A3AD178514960F1079E3831733AA24F254C17BADF02FAE13732F5`.

Commands: `Invoke-AdverseValidV11.ps1 -Version 20 -Step All` passed execution/
lineage/rollback assertions while retaining failed migration status;
`adverse_valid_v11_evidence.py --version 20` intentionally fails acceptance;
`verify_series_fallback_v20.py` passes actual physical fallback proof.
SDK10 API/runner builds and **103 migration +5 Hangfire tests pass, zero skipped**.

## Still not accepted / not implemented as coverage

Operations/auth/scoped controls, complete diagnostic CSV/Markdown, guarded
preview/restricted impact/confirmed inverse and primary writers are implemented.
Remaining independent release/security/concurrency/inverse/photo/OT/SS-V001..057
coverage is not complete. Strict ready-v4 Report remains18 exact/39missing/
15unexpected response rows, not39 lost records; UI/CSV/PDF parity is not executed.

Original extractor SQL is now located in the workspace; the unchanged repo-local
handoff directory remains empty. **Exact original SH01–08 execution has not run.**
Local4631 comparable +1222 suppressed bounds +53 explicitly withheld noncomparables
must not be called an original-query pass. Parent-owned canonical refresh/mirror
also remains pending supplied current inputs.

No shared app DB reset, original fixture deletion, app workflow/JC-default edit,
tenant GitOps change, generated web-client edit, attribution trailer or push.
