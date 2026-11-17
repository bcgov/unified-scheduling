# Complete matched response differences — parent-document summary

Measured ready-v4 Report: **57 source /33 target, 18 exact, 39 missing /15 unexpected**.
Strict response rows, **not lost business records**. Three windows repeat some facts.
Independent source/actual target services and crosswalk normalization were used;
no SS status/availability formulas were reconstructed.

**Supplemental execution now available:** the “22 unexecuted” classification
below describes the retained strict Report at capture time, not the current
execution inventory. [Actual common-service reads](2026-11-16-actual-distribution-reads.md)
subsequently executed all 19 distribution observations: resource presence is
4/4 exact; 15 source events versus 9 target events give 8 exact, 7 missing and
1 unexpected common-projection row. All 33 window/location call groups completed
without read failures. Full distribution fields remain unexposed; the other
3 training harness projections are not executed UI/CSV evidence on either side.
These narrower reads do not rewrite the strict Report or claim full parity.

| Surface | Source | Target | Exact | Paired differing rows | Other missing rows |
|---|---:|---:|---:|---:|---|
| Duties |6|6|6|0|0|
| Duty slots |6|6|0|6|0|
| Shifts |7|6|0|6|1 merged-member alias |
| OT allocation |7|6|6|0|1 merged-member alias |
| OT calculated |6|6|6|0|0|
| Training API |3|3|0|3|0|
| Training harness projection |3|0|0|0|3 unexecuted target projection |
| Distribution conflicts |15|0|0|0|15 unexecuted equivalent target adapter |
| Distribution sheriffs |4|0|0|0|4 unexecuted equivalent target adapter |
| **Total** |**57**|**33**|**18**|**15**|**2 aliases +22 unexecuted** |

All15 unexpected target rows pair to source identities: six slots, six shifts, three
training rows. Each also accounts for one strict missing source row.

Complete22 unexecuted target-surface rows by actual source capture window:

| Surface | pastmonth | cutover-minus14-plus14 | futuremonth | Total |
|---|---:|---:|---:|---:|
| Distribution conflicts |10|3|2|15|
| Distribution sheriffs |2|1|1|4|
| Training harness projection |1|1|1|3|
| **Total** |**13**|**5**|**4**|**22**|

These are absent equivalent target adapters/exports, not22 proven lost business
records. The projection is not executed source or target UI/CSV evidence. Counts
were independently grouped from the retained matched57-row source capture.

| Paired surface/field | Differing field cells | Source → actual target | Classification |
|---|---:|---|---|
| Slot `closed` |6|false → unexposed/null | API/model exposure, no true-flag case measured here |
| Slot `notAvailable` |6|false → unexposed/null | Same |
| Slot `notRequired` |6|false → unexposed/null | Capacity exported separately, never used to invent SS flag |
| Slot `overtime` |6|false → unexposed/null | Same exposure gap |
| Shift `overtime` |6|source value → unexposed/null | No equivalent field; not reconstructed |
| Shift `slotIds` |6|source member IDs → unexposed/null | Merged/crosswalk identity exposure |
| Shift `workSection` |6|source value → unexposed/null | No equivalent field |
| Shift `end` |1|historical representative ends12Z → merged block16Z | Group union08–16Z exactly preserved |
| Training `excused` |3|false → unexposed/null | Business eligibility/policy exposure gap; true-Excused parity unproved |
| Training `rowVariant` |3|white → unexposed/null | Presentation |
| Training `status` |3|blank → Active | Derived compliance output, not stored enrollment state |
| **Total differing paired field cells** |**52**|Across15 paired rows |Not52 lost records |

Observed training **stored facts match** for one completion repeated in three windows:
user/course/location/region, end `2026-11-01T10:00:00Z`, null expiry. Target `Active`
comes from actual completion/expiry/current-clock compliance logic. Source metadata
also evaluates current clock; no clock-mismatch explanation is claimed. Blank-vs-Active
alone does not prove a wrong qualification; neither does it establish broad compliance
parity. BL13 future expiry-policy and BL14 per-record date display remain open.
Neither harness projection is executed UI/CSV evidence.

Historical missing shift/OT alias900001 belongs to the two-to-one08–16UTC merge;
not a missing working interval. All observed OT values are zero in this tiny fixture,
so no broad overtime-policy pass follows.

## BL15 — primary future scheduling blocker, no migration workaround

Matched future source `ss_ready_v4_probe` / actual Unified target: **12 calls,
two candidate-presence differences, four conflict-count/interval differences**.
Both home/destination inside/whole-day SS calls return one Away with exact
December10,2026 **10–12UTC**, timezone null; target proposed-shift conflict responses
return zero. Target destination discovery includes the user in strict-before/
strict-after windows where SS omits them. Exact target home calendar interval is
preserved but does not restore discovery/conflict semantics.

Proposed **application** decision/backlog, outside migration scope:

1. Make LoanOut borrowed-user discovery accept/use instant-sensitive intervals instead
   of date-only inclusion. Preserve measured boundary-touch discovery separately:
   SS includes the destination at a touching boundary but excludes strict-outside.
2. Register/align LoanOut overlap participation in actual scheduler conflict responses.
   Positive-duration overlap and returned interval provenance must be explicit, rather
   than returning no Away conflict or expanding it to a whole-day response.
3. Review the day-based supplied-candidate assignment guard against interval-sensitive
   home/destination semantics. Test discovery, conflict display and actual save-validation
   independently, including cancellations, timezones and boundary cases.

**No app fix or migration-side post-filter/waiver was implemented.** Target home guard
rejects all six supplied candidates; SS availability returns home presence and zero
Away conflicts outside the interval. Source write/UI/save behavior was not captured:
do not turn this into invented free-minute totals or proven source save authorization.
Future scheduling acceptance remains Fail/NeedsAction pending the application decision.

Evidence/hashes/provenance: [matched Report](2026-11-16-matched-report.md) and
[matched future observations](2026-11-16-graph-replay.md).
