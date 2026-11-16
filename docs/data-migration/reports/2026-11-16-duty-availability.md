# Duty replay and actual availability follow-up

Not migration acceptance. Production remains disabled. No target application behavior
was changed. Source `ss_ready_v4` and original adverse `ss` were not mutated.

## Duty update correctness

Duty mapped-source hashes now include their mapped Comment and complete relevant
source Shift identities/intervals/location/user/timezone/expiry. Previously a comment-only
edit or a changed source Shift could leave an unchanged Duty row's assigned-shift graph
stale. These are source dependencies, not mutable target values masquerading as source.

BatchSize=2 exposed a real navigation bug: the update callback loaded overlapping Shift
rows without Event, then used Event in memory. Change-tracker clearing at committed
batch boundaries made six Duty updates fail with NullReferenceException. The callback
now explicitly includes Event. Failure `fbdc5618-b108-4dcd-aea5-9cbbacfc1547` remains
retained; no source rows were falsely marked migrated.

Actual PostgreSQL, dedicated `ss_ready_v4_probe` source:

- `dcc9a1a5-761f-4c86-afa6-7764663b869f`: Completed 24, six updated roots,
  zero inserted roots/rejects/failures.
- Guarded fixture script `docker/migration/change-duty-comment-probe.sql` changes only
  source Duty900000 Comment/change timestamp. It refuses any database other than the
  exactly named, marked synthetic probe. The oracle-ready source is untouched.
- `2336aabe-1b5c-466d-b506-8f4ca84f58d7`: Completed 24, **one updated root**,
  zero inserts/rejects/failures. Physical journal has exactly one AssignmentEntries U
  and one Events U, no replacement of those roots. Private value equality and current
  crosswalk application-receipt checks were both true; text was not printed.

## Further inverse defects found and fixed

Reversing the comment-only replay initially failed SQL23505: newly created auxiliary
ShiftAssignmentEntry rows occupied the deleted originals' unique keys. The inverse now
deletes confirmed created leaf rows in FK order before restoring originals. Surviving
original FK dependents defer early deletion until their own references are restored.
No constraints are disabled. The failed attempt is retained separately and committed
no partial inverse.

Successful actual inverses:

| Original | Receipt | Footprint |
|---|---|---|
| comment-only replay2336aabe | `518a471f-6daf-479b-8fc9-d1f205bfd528` | 40 distinct journaled rows, including replacement links |
| full Duty replaydcc9a1a5 | `94564c2d-e85c-4f6a-b585-52131b72f1b1` | 89 distinct rows |
| partial failed runfbdc5618 | `e46033db-c3c9-4285-9d06-aafbe64402e2` | 42 distinct rows |

Chained inverses also exposed conservative xmin-only rejection after mapped values
had been restored: inverse intentionally preserves Unified audit timestamps. Retained
run `3f47f215-6430-44fb-b726-096337f48698` rejected Duty900000 and its dependent slot.
QX4 now follows canonical mapped-value equality under row/Event locks for non-User
targets. **User still requires the exact recorded version to fence unhashed private
photos.** The full mapped SHA-256 comparison remains mandatory, so unrelated mapped
text/business drift is not blessed. A prior mutation/version proof is still required.
Inverse receipt renewal similarly permits photo-absent candidates only after checking
their entire restored mapped hash; photo-bearing candidates retain strict physical proof.

Exact-ready source reapplication:
`7794b9e5-7293-46e6-ac90-6fd39d4bffb3`, Scheduling Completed23, zero rejects/failures.
Current ready lineage on isolated rollback target:
MyTeam `00aa4a2a-12a1-4400-99fd-9a0bb38e9bc6`,
Training `26f489b1-7566-4796-b78d-d2629afb7372`,
Scheduling `7794b9e5-7293-46e6-ac90-6fd39d4bffb3`.
This is not a Report PASS or a claim that repartition/changed-series replay is complete.

## Actual target availability evidence

New read-only runner command `availability-probe` executes twelve actual
`ShiftLoanOutAvailabilityService.GetAssignableUserIdsByOccurrenceAsync` calls:
home/destination × strict-before, boundary-before, inside, boundary-after, strict-after,
whole-range. It verifies the actual selected source database comment `SS SYNTHETIC`,
requires an explicitly allowed ready-v4 source/isolated target, normalizes identities
through crosswalks, and writes exclusive owner-readable evidence without source text,
photos, credentials or inferred SS outcomes.

Restricted ignored artifact:
`docker/migration/artifacts/target-availability-ready-v4-source-window-v1.json`

SHA256:
`82817B681FB276BCDB833BF1FBCCF1433F95BF43DFE2DBC1007C08809BC2FE6A`

- Verified actual source database: `ss_ready_v4`; away900000, source User
  `11111111-1111-4111-8111-111111111111`; home900000, destination900001.
- Probe range 2026-11-03T08Z to2026-11-04T08Z; inside10–12Z.
  Strict probes exclude the source boundary by one PostgreSQL microsecond.
- **Actual home-location timezone is Etc/UTC**, not the earlier assumed Vancouver
  fallback. Both the resolved target timezone and exact UTC interval are recorded.
- The target filter rejects this supplied user at home in all six calls, and permits
  the supplied user at destination in all six calls.
- Crucial scope: this API filters an explicitly supplied candidate; it does not
  discover all available sheriffs. Destination true is not evidence that the full
  target picker discovers that user. It also does not return SS conflict intervals,
  free minutes, UI availability or CSV/PDF evidence. No such claim is made.
- Parent-provided actual SS source observation is zero home conflicts outside10–12Z,
  one Away inside; destination strict-outside user absent/boundary user present.
  The home guard has an observed temporal difference; full workflow equivalence
  and destination discovery still require matched response-contract comparison.
  Do not convert1320 target-only suppressed minutes into an asserted SS workflow loss.

The earlier immutable default-day target artifact
`target-availability-unified_ss_rehearsal_ready_v4_rollback.json`
used actual UTC local-day00–24 bounds, not08–08. It remains retained, SHA256
`55EBA86DA72C944138BDDB7D175C9F0706F47746D511F2527EDDC1FD4BC9C440`.
Use the explicit source-window artifact above for the requested interval comparison.

## Validation and remaining work

SDK10 runner build passes. Latest focused DataMigration/Hangfire execution:
76 passed, zero failed/skipped. These operational PostgreSQL probes are not added to
xUnit counts. UnitTests broader integration coverage remains a follow-up.

Still required: complete multi-root merge/split/repartition, multi-entry Duty graph
updates, changed-series replay, isolated parent dry-run candidates, measured SH fit/
exact fail-bucket reporting, Report inverse/adverse coverage, matched full independent
source/target Report and canonical mirror refresh. The requested source-all and
availability files have not yet appeared in this repository's ignored artifact directory;
their mirror is coordination pending, not an excuse for unfinished implementation.
