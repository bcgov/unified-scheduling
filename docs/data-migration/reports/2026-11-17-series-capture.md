# Series fallback, closed-slot replay and bounded initial capture

**Migration-owned changes only. Production disabled; full acceptance NO-GO.**

## Exact retained adverse causes

Read-only inspection of immutable v13 found all **33** rejected AssignmentSeries
had `InconsistentSeriesMember`: all 33 groups contained mixed mapped capacities
(closed/not-required single slots map to zero, other entries to one). None had
mixed locations or multiple live slots. Rejecting these otherwise valid standalone
duties was a migration fallback bug, not an external acceptance blocker.
Private cause receipt SHA256:
`97B4C061C35E2EA813E168F02D0252AF2529C00D936EA16FC2EEA8E048EED558`.

The one failed physical DutySlot was `MaterialMissingShiftUserLink`, **2026-12-03
17:00–23:00 UTC**, America/Vancouver. Its source user exists; it is not one of the
manifest expectations. Both overlapping source shifts were held as
`FutureGapRequiresSourceRepair`, so neither supplied an admitted target shift/user
link. No shift, time or user link may be invented to hide this dependency. The
other v13 failed outcome was the actual future publication validation; neither
failure is waived by the series fix.

## Implementation

Before creating/composing a series, compare all mapped child definition, capacity,
classification and location fields. Inconsistent groups retain their real entries
and receive `Excluded/StandaloneInconsistentMember`. Original owner/hash/version
guards still execute before fallback. Linked-only and drifted entries still reject.
The existing in-composition consistency check remains a defensive invariant.

Actual PG replay exposed another bug: a closed/flag-only slot skipped crosswalk
rebind because its old record already existed. That left a live slot unresolved
after replacing its parent. The generic slot-link path now distinguishes existing
retained receipts from receipts explicitly marked for atomic rebind. Source
primary crosswalk IDs remain stable; no primary record is deleted.

Initial graph capture formerly transferred all scheduling root crosswalks once
per disconnected component (12,189 components; sampled worker received 368GB).
It now builds one root-ID membership index, including every DutyEntry segment and
AssignmentSeries alias. Each component still reloads its exact receipt IDs inside
its own transaction and runs the existing live target hash/version/dependent
guards. Neither ownership receipts nor target hashes are cached. Changed-component
recapture uses fresh selection, not the initial index. Full-source/group planning
and changed-component lookup remain separate performance opportunities.

## Actual PostgreSQL regression: additive v17

Source `ss_series_v17` is a new clone of ready-v4; target
`unified_ss_rehearsal_series_v17`. Original sources/targets were not modified.
Actual EF DML, real recurrence expander, encrypted inverse journal and BatchSize2.

| Phase | Scheduling run | Actual result |
|---|---|---|
| Initial mixed capacities | `59c0bb3e-6cb7-4441-90ad-a5364bd6c1bd` | Completed23, zero rejected/failed; real children standalone |
| Source becomes uniform | `81e36253-36b9-4d09-b18d-7235468931f3` | Completed23; exactly two actual source occurrences in one series |
| Measured pre-fix closed-slot replay | `eab30c07-6518-4e9a-9997-a0efcc8ada12` | Failed, seven rejected with GraphUnresolvedLiveSlot; retained, not relabeled |
| Retry after slot-rebind fix | `2076502c-1496-4f04-9635-ee9a435439b6` | Completed23, zero rejected/failed; series removed, real standalone entries preserved |
| Independent target edit + changed source | `6b6029a5-b003-4b1a-9c17-ef8ccc29783d` | Failed, seven rejected OwnedTargetDrift; exact whole graph footprint unchanged |

Each successful phase asserted exact real occurrence IDs, no orphan
entry/event/series, and no dangling/colliding crosswalks. The independent edit is
deliberately retained. An initial assertion incorrectly counted a separate valid
baseline series; it failed after the successful first run. Corrected group-scoped
assertions resumed that retained baseline without reset or fabricated results.

Reproducer: `docker/migration/Invoke-SeriesV17.ps1`, fresh initialized v17 only.
Its narrowly guarded resume switches exist for the retained harness and measured
pre-fix failure, not arbitrary filled-database replay. A separate `/tmp` build was
used for the slot fix while the full v16 worker retained its original assemblies.

## Checks and full follow-up boundary

SDK10 runner/API builds pass. **101 migration +5 Hangfire tests pass, zero
failed/skipped.** Two added index regressions compare complete membership to the
previous predicate over 500 disconnected groups/3,500 roots, including multi-entry
aliases, duplicates, malformed prefixes and source isolation.

The optional full v16 profiling attempt was stopped through the actual audited
job-abort operation while still in Shift DML (BatchSize250). Its rollback and
Running-child Report guard passed; its final coverage verifier correctly failed
because the interrupted Scheduling writer did not finish. It is retained, not
coverage. It predates the separately tested closed-slot rebind correction.

A first v18 parent at BatchSize10 exposed expensive source timestamp-page sorts,
not target DML. It completed but is superseded for the child lineage. A new source
`ss_adverse_valid_v18` was cloned by PostgreSQL from unchanged v11 and received
only `(COALESCE(UpdatedOn,CreatedOn),Id)` paging indexes. No business rows or
expectations were changed; original v11 remains untouched. Indexed parent
`181de33d-96ea-4400-86c6-3b786e29b34c` completed2,233 with zero rejects/failures.

The v18 child profile was also stopped through audited abort after exposing missing
reverse target lookup indexing. Its physical rollback passed; final coverage
assertion correctly failed. It is not full adverse coverage.
[The metadata-only reverse index and actual100k plan proof](2026-11-17-target-lookup-index.md)
are now implemented. Fresh full v20 uses all four changes (batch size, source paging
indexes, initial target-root membership index and reverse lookup index). No runtime
improvement may be attributed solely to one fix or called production sizing acceptance.
The [completed full v20 proof](2026-11-17-adverse-v20-final.md) verifies all33 groups
and20,224 actual standalone Duty roots, zero rejected/two failed Scheduling outcomes,
one full-root read and12,189 fresh component reads. Runtime4h30m50s; not a business
or production-sizing pass.
