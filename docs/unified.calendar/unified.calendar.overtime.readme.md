# Unified.Calendar - Projected overtime business rules

## Purpose and scope

Unified Scheduling calculates projected worked time, credited time and potential overtime
from scheduling information. These rules record the business decisions confirmed by the
Sheriff client and govern the working-hours calculation used by the scheduling calendar.

This is a projection of scheduled activity, not a record of confirmed attendance or a payroll
entitlement calculation. It does not define additional BCGEU rules or represent the complete
collective agreement.

For the shared calendar infrastructure, see [Unified.Calendar](unified.calendar.readme.md).

## Definitions

| Term | Agreed meaning |
|---|---|
| Authoritative shift | The sheriff's single published shift for the business day; it defines the regular schedule and the boundary for associated outside-shift assignments. |
| Regular scheduled working time | The shift's elapsed duration less available unpaid lunch, before replacing scheduled work with leave or adding worked lunch. |
| Available unpaid lunch | The number of unpaid lunch minutes recorded on the shift. These minutes are excluded from regular scheduled working time. |
| Worked lunch | The recorded portion of available lunch scheduled to be worked, subject to the existing effective-worked-lunch adjustment when leave reduces remaining work. |
| Outside-shift assignment time | The distinct minutes of the sheriff's associated assignments before the shift starts or after it ends. |
| Paid leave | Leave classified as paid that replaces scheduled working time. It contributes to credited time, but is not worked time. |
| Unpaid leave | Leave classified as unpaid that replaces scheduled working time and contributes neither worked time nor credited time. |
| Projected worked time | Scheduled work remaining after leave adjustments, plus effective worked lunch and associated outside-shift assignment time. |
| Credited time | Projected worked time plus paid leave. Worked and credited hours therefore differ whenever paid leave is present. |
| Configured regular working-day threshold | The configurable daily credited-minute limit used to calculate projected overtime, independent of an individual shift's duration. The default is 420 minutes (7 hours). |
| Projected overtime | Credited time exceeding the configured regular working-day threshold, with a minimum of zero. |

## Business rules

1. A sheriff has one published shift per day, and that shift defines their regular schedule.
2. A shift specifies available unpaid lunch and how much of that lunch is worked.
3. Worked lunch contributes to projected worked time but does not automatically generate overtime.
4. Assignment time outside the associated shift contributes to worked time.
5. Overlapping assignment intervals must not be double-counted.
6. Paid leave contributes to credited hours and counts toward the overtime threshold.
   **Paid leave counts toward the daily overtime threshold, even though it is not actual worked time.**
7. Unpaid leave does not contribute to credited hours.
8. Projected overtime is the amount by which credited hours exceed the configured regular
   working-day threshold.
9. **Work outside the shift does not automatically generate overtime when credited time remains
   below the configured threshold.**
10. The calculation represents projected scheduling overtime, not confirmed attendance or
    payroll entitlement.

## Calculation algorithm

Calculate minutes for each authoritative shift and sheriff, retaining the existing business-date,
user and shift-location filters. Use elapsed UTC minutes for durations and the shift's time zone
to determine its starting business date.

1. Subtract available unpaid lunch from the shift duration, with a minimum of zero, to obtain
   regular scheduled working minutes (`RegularMinutes`).
2. When the Leave feature is enabled, reconcile the sheriff's applicable leave against the
   shift interval. Clip leave to that interval and merge overlaps so that a minute replaces
   scheduled work only once. If paid and unpaid classifications overlap, classify that overlap
   as unpaid. Cap unpaid leave at regular scheduled working minutes, then cap paid leave at
   the remaining regular minutes. Full-shift leave therefore excludes unpaid lunch.
3. Subtract paid and unpaid leave from regular scheduled working minutes to obtain remaining
   regular work. The effective worked lunch is the recorded non-negative worked lunch capped
   at the remaining regular work. Full-day leave leaves no effective worked lunch.
4. Merge overlapping or touching assignment intervals associated with the shift and sheriff.
   Sum only their portions outside the shift as `PaidOutsideShiftMinutes`. An interval inside
   the shift adds no extra minutes, including when it overlaps leave. Assignments do not
   override the existing leave reconciliation.
5. Calculate projected worked time, credited time and overtime using the following formulas.

```text
RemainingRegularMinutes =
    RegularMinutes - PaidLeaveMinutes - UnpaidLeaveMinutes

EffectiveWorkedLunchMinutes =
    Min(Max(0, RecordedWorkedLunchMinutes), RemainingRegularMinutes)

InShiftWorkedMinutes =
    RemainingRegularMinutes + EffectiveWorkedLunchMinutes

WorkedMinutes =
    InShiftWorkedMinutes + PaidOutsideShiftMinutes

CreditedMinutes =
    WorkedMinutes + PaidLeaveMinutes

OvertimeMinutes =
    Max(0, CreditedMinutes - FullWorkingDayMinutes)
```

`WorkingHoursOptions.FullWorkingDayMinutes` is the configurable threshold. It defaults to
420 minutes and must be a positive integer. The existing `WorkingHours` configuration section
binds and validates this option at startup. For example:

```json
{
  "WorkingHours": {
    "FullWorkingDayMinutes": 480
  }
}
```

The threshold must not be derived from shift duration. Outside-shift work and worked lunch
have no separate overtime classification. When the Leave feature is disabled, leave does not
adjust work or credit; the same credited-time threshold still applies.

## Client-confirmed examples

**The following four outcomes are confirmed client business decisions, not hypothetical examples.**
All assume a 09:00-17:00 shift, one hour of unpaid lunch (illustrated as 12:00-13:00),
and a configured regular working-day threshold of 420 minutes (7 hours).

| Scenario | Regular work | Worked lunch | Outside-shift work | Worked | Paid leave | Unpaid leave | Credited | Projected overtime |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 1. Paid leave with worked lunch | 3h | 1h | 0h | 4h | 4h | 0h | 8h | **1h** |
| 2. Paid leave with outside-shift assignment | 3h | 0h | 1h | 4h | 4h | 0h | 8h | **1h** |
| 3. Unpaid leave with outside-shift assignment | 3h | 0h | 1h | 4h | 0h | 4h | 4h | **0h** |
| 4. Five hours paid leave with outside-shift assignments | 2h | 0h | 3h | 5h | 5h | 0h | 10h | **3h** |

1. Three hours of regular work plus one worked lunch hour produce four worked hours. Adding
   four paid leave hours gives eight credited hours, one above the seven-hour threshold.
2. Three regular work hours plus one outside-shift assignment hour produce four worked hours.
   Four paid leave hours bring credit to eight hours, producing one projected overtime hour.
3. Three regular work hours plus one outside-shift assignment hour produce four worked hours.
   The four unpaid leave hours add no credit, so credit remains below the threshold and
   projected overtime is zero.
4. Two regular work hours plus three outside-shift assignment hours produce five worked hours.
   Five paid leave hours bring credit to ten hours, three above the threshold.

Under the same assumptions, a normal seven-hour working day produces zero projected overtime;
working the full lunch hour produces eight credited hours and one projected overtime hour.
Full-day paid leave produces seven credited hours and zero overtime; full-day unpaid leave
produces zero credited hours and zero overtime.

## Important distinctions and limitations

- Projected overtime is a scheduling estimate. Actual attendance and payroll entitlement
  require their own authoritative processes.
- Paid leave increases credited hours without increasing worked hours. Unpaid leave
  increases neither. Leave replaces scheduled work rather than duplicating it.
- Worked lunch adds effective worked minutes. It generates projected overtime only to the
  extent that total credited minutes exceed the threshold.
- Outside-shift work adds distinct worked minutes. Crossing a shift boundary alone does not
  establish overtime entitlement or projected overtime.
- The agreed calculation assumes one published shift per user per business day. The current
  service returns a result per shift/user; it does not reconcile multiple shifts into a new
  daily schedule or change existing publication/status filtering.
- Lunch is recorded as available and worked minute totals, without a separately timed lunch
  interval. The 12:00-13:00 lunch above illustrates the agreed scenarios; it is not a stored
  interval. Partial leave is treated as coverage of scheduled working minutes, capped at
  regular scheduled working time. The service cannot identify actual lunch/leave overlap.
- Historical shifts with either recorded lunch value missing retain unknown calculations:
  worked, leave, credited and overtime totals are unavailable (`null`). Outside-shift
  assignment minutes remain calculable. Explicitly recorded zero lunch is known information
  and permits calculation; it is not treated as missing lunch or replaced with a default.

## Reporting consistency

Future working-hours and overtime reports must use the same backend calculation as the
scheduling calendar: `Unified.Scheduling.Services.WorkingHoursService`. Reporting and frontend
components must not duplicate overtime formulas or independently classify outside-shift work
or worked lunch as overtime. They must preserve the distinction between worked and credited
time, the configured threshold, and unavailable historical results.
