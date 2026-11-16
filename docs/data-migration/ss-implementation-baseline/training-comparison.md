# Training Export And Code Comparison

**Baseline 2026-11-16.** SS behavior traced from source; runtime comparison pending until
the oracle and migration run. This document does **not** claim exported-data parity.

## What The SS Export Actually Represents

The [report service](../../legacy/sheriff-scheduling/api/services/usermanagement/TrainingService.cs:68)
returns one selected history per enabled, non-exempt sheriff/course, not all training records.
It filters on current home location/region and current permissions even for historical reports.
Only active courses (`LookupCode.ExpiryDate == null`) and non-deleted individual histories
(`SheriffTraining.ExpiryDate == null`) participate. Certificate expiry is a different field.

Selected mandatory courses create Not Taken placeholders when history is missing.
Optional courses without history produce no row. Course Description supplies the report label.
History selection: latest EndDate normally (even future); historical mode uses latest EndDate
strictly before report end, else oldest history. Start date only enables historical mode.
Equal EndDate ties have no explicit ID tie-break, so differing tied records are ambiguous.

The [report UI/export](../../legacy/sheriff-scheduling/web/src/components/MyTeam/ViewReports.vue:439)
excludes Excused rows from CSV, though the API returns them in a separate grouping. Status
filters apply to non-excused rows. All filtered rows, not just current page, export:
Location, Name, Training Type, End Date, Expiry Date, Status, Region.
Dates display as DD Mon YYYY in the history timezone (null falls back Vancouver).

## Data Mapping And Intended Differences

| Business item | Migration contract / comparison |
|---|---|
| User/course identity | Real source-ID crosswalks; generated target IDs. No matching by report name |
| Completion interval | StartDate -> AwardedOn; EndDate -> EndingOn; equality allowed |
| Certificate expiry | Exact stored TrainingCertificationExpiry including null; never recalculate |
| Course retirement | Copy to course; eligible historical completions still migrate, even if SS export hides retired course |
| Deleted individual history | Excluded, counted, no new target/link; not certificate expiry |
| Excused sheriff | User migrates, all training intentionally excluded; SS CSV also excludes it. API grouping gap BL-2 |
| Repeats | All eligible histories migrate distinctly; versions stable by End/Start/source ID after existing max |
| Other course label/notes | Preserve labeled Comment plus distinct Note; no text trimming |
| Missing mandatory course | Report placeholder is not proof of completion; migration must not create qualification |
| Standard auto-created history | Existing SS rows migrate as stored facts, not claimed attendance evidence; do not generate new qualifications |
| Notice state | FirstNotice -> None/Sent; no Pending or migration-triggered email |
| Categories | Approved SS exact labels preserved; not Stats classifications |
| Carbine profile | Only eligible in-cutoff, non-excused, non-deleted Carbine history qualifies; preserve existing non-null profile |
| Future completions | Preserve dates; compare actual report visibility/status, do not rewrite dates to force equality |
| Disabled/exempt users | Data still migrates where approved; report population differences must be explained, not lost histories |

## SS Status Is Derived, Not Attendance Data

The [status code](../../legacy/sheriff-scheduling/api/services/usermanagement/TrainingService.cs:220)
uses stored certificate expiry R. Effective expired boundary E is R for rotating training,
otherwise R plus one year. Its report rotating test includes the validity-days list, not just
the explicit Rotating flag.

Current mode, in order: now>E -> Expired; now>R -> Requalification; inside advance-notice
window and FirstNotice -> Notified; else blank (CSV Qualified). Boundaries are strict `>`.
Null R normally yields Qualified, including future completions.
Historical mode uses report end instead of now, never Notified, and can yield Not Taken
for a selected future completion. Range-end selection itself uses strict `<`.

These are SS display rules, not status fields to import. Preserve exact business dates and
record actual Unified status differences; BL-1 needs Josh's review before any grace-year fix.
BL-7 covers AwardedOn/EndingOn meaning and expiry-base naming.

## Deterministic Validity Cases

Run actual SS service and actual Unified report, not a copied formula. Freeze comparison time
or use historical mode with explicit offsets. Map report rows through source user/course IDs.

| Case | Expected SS evidence / migration check |
|---|---|
| Mandatory and optional course, no history | Mandatory Not Taken only; no invented target qualification |
| Deleted history or retired course | SS hides relevant row/history; migration exclusion/course rules remain explicit |
| Excused / disabled / active exempt | CSV omits them; distinguish source API population from target history storage |
| Two repeats around report end | Latest strictly-before end selected; full target history still has both |
| Equal end dates with differing facts | SS winner unspecified; flag ambiguity, no fabricated deterministic parity |
| Null certificate expiry | Copy null; no forced expiry calculation or notification |
| Non-rotating expiry + one year | Explain status difference without changing migrated dates |
| Notice false vs true | None/Sent preserved; report difference classified, no CHES call |
| Completion 2026-01-01T07:30Z, Vancouver | Export date 31 Dec 2025; stored instant unchanged |
| Equal Start/End | Allowed training instant, not rejected generic event interval |
| Comment and Note | Other label appears once and distinct Note retained |
| Earlier-cutoff or late older completion | Recovered without duplicate links or renumbering |

## Measured Results

Not yet run in this session. Populate actual source/target row and field counts, report
windows, ignored layout differences, matched discrepancy classes and artifact/command links
after implementation. Any missing business field becomes BL-11 with concrete affected counts.
Unexplained discrepancies mean Fail, not PassWithKnownDifferences.
