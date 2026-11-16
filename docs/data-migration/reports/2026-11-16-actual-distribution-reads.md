# Actual target reads for the 22 previously unexecuted rows

**Full matched Report remains FAIL. This is a declared common projection, not
full distribution parity, lost-record counting, or UI/CSV/PDF evidence.**

Exact source `ss_ready_v4`, captured source SHA256
`DC28370841D1B444D3950E897EDEE2AA268ACC1EDAD7DFDCCDEECA2B1EDC62C7`;
applied target `unified_ss_rehearsal_ready_v4_rollback`.
The probe requires exact database identities and every target connection defaults
to PostgreSQL read-only. Source/target identities resolve through real crosswalks.
No application behavior or SS reconstruction was introduced.

## Actual methods and read regression

`IUserService.GetAllAsync`, `ILoanOutCalendarDataProvider.GetUsersAsync`,
`ISchedulingCalendarService.GetDataAsync`, `TrainingCalendarEventService.GetEventsAsync`
and supplemental `ICalendarConflictService.GetConflictsAsync` ran for **33 actual
window/location groups**: 11 mapped locations × the three captured source windows.
All 33 supplemental pairwise-conflict calls completed, returning zero pairs.
Pairwise conflicts are **not** SS distribution availability-event rows and were
not substituted for them.

The first retained v11 probe passed nonzero-offset source window values directly
to Npgsql timestamptz parameters; all 33 conflict calls failed. Their safe method
metadata identified `DateTimeOffsetConverter.WriteCore`. The migration adapter
now uses `ToUniversalTime()`, preserving the exact instants. Corrected v12 has
**zero read failures**, independently checked by `report_surface_evidence.py`.
This was a probe bug, not an application blocker. Neither artifact was overwritten.

Ignored restricted receipts:
`docker/migration/artifacts/target-report-surfaces-v11.json`,
`target-report-surfaces-v12.json` and `target-report-surfaces-v12-differences.json`.
No private rows are committed.

## All 22 source rows accounted

| Formerly unexecuted surface | Source rows | Actually executed target equivalent | Result / boundary |
|---|---:|---|---|
| Distribution resource presence | 4: past 2, cutover 1, future 1 | Actual home + borrowed user discovery | All 4 match on `(window, query location, user)` |
| Distribution availability events | 15: past 10, cutover 3, future 2 | Actual scheduling + training calendar reads | Target 9; common projection 8 exact, 7 missing, 1 unexpected |
| Training harness projection | 3: one per window | No executed source UI surface exists in this capture | Not UI evidence on either side; cannot certify UI/date-display parity |

Common event projection is only `(window, query location, user, kind, start, end)`.
Target resource results expose **no start/end span**. `conflictLocationId`,
timezone, workSection, overtime, eventType and slotIds remain outside this
comparison. `ScheduleDistributionService` sends PDF/email; it has no equivalent
SS distribution-read DTO. Delivery was not invoked to manufacture read evidence.

## Exact event differences, all in pastmonth

| Kind / query location | Missing source rows | Unexpected target rows | Actual classification |
|---|---:|---:|---|
| Shift / home | 2 | 1 | Source Nov 2 08–12 + 12–16 UTC versus one target 08–16 UTC. Required touching merge preserves interval union; not two lost records |
| Shift / destination | 2 | 0 | Source exposes both Nov 2 source segments for the borrowed resource; destination target calendar does not expose them |
| Training / destination | 1 | 0 | Source Nov 1 10:00 UTC point completion absent from destination target calendar; stored home completion facts still match |
| LoanOut / destination | 1 | 0 | Source Nov 3 10–12 UTC Away absent from destination target calendar; exact home LoanOut event exists |
| Leave / destination | 1 | 0 | Source Nov 4 08–16 UTC leave absent from destination target calendar; home leave exists |
| **Total** | **7** | **1** | Not a claim that seven stored business records were lost |

The remaining eight common event rows match: pastmonth home training/LoanOut/leave,
three cutover shifts, two future shifts. Destination resource presence alone does
not prove that its calendar exposes the resource's home events. These historical
destination-view differences need a Scheduling/LoanOut distribution-contract
decision; a migration adapter must not clone events to force matching.

## Strict Report and primary blocker are unchanged

The original strict comparison is still **57 source / 33 target / 18 exact /
39 missing / 15 unexpected response rows**. This additional limited projection
does not rewrite that Report or waive unexposed fields.
[The complete paired-field table](2026-11-16-matched-differences.md) remains valid:
slots 6 pairs/24 differing cells, shifts 6/19, training 3/9, plus 2 merged aliases.
Training's 9 cells are exactly `excused false→unexposed null` ×3,
`rowVariant white→unexposed null` ×3 and derived `status blank→Active` ×3.
Stored completion/course/user/home/region/end/expiry facts match; this is not broad
true-Excused, policy-compliance or UI parity proof.

**BL15 remains a separately measured primary future scheduling blocker:** the
matched future probe has 2 destination candidate-presence differences and 4
missing Away conflict responses across 12 actual source/target calls. Source
inside/whole-day returns only Dec 10 2026 10–12 UTC, timezone null. Exact target
home calendar storage does not restore proposed-shift API semantics. Proposed
app work remains interval-sensitive LoanOut discovery, conflict participation
and guard alignment. No app fix, migration workaround or business-loss waiver.
