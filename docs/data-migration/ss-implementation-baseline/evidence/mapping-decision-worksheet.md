# SS Migration: Remaining Decision Worksheet

SS only; CASS remains deferred. Independent review: see
[Review Record](#review-record).

This worksheet gathers mapping choices and records user decisions. It does not by itself
approve implementation, migration tests, database writes, or branch changes.

## How To Use

- **A:** mapping choices that can be reviewed now from code, models, and the supplied snapshot.
- **B:** mappings still parked for live-data validation, client approval, or Types 1-4/Stats work.
- **C:** engineering design choices, not business mappings.
- **D:** deferred checks and already-approved implementation gates; not new mapping questions.
- Rule Proposed cells are proposals unless a user decision is recorded. Reconcile
  accepted decisions into the canonical contract and existing review records.
- `Q-E3` through `Q-E10`, `Q-R1` through `Q-R8`, and `Q-P7` are existing question IDs.
  `Q-L*`, `Q-E11`, `Q-P8`, `Q-R9`, `Q-R10`, `Q-X3b`, `Q-T*`, `Q-S1`, and `EN-*` are new
  worksheet-only labels. `Q-X3b` identifies the unresolved assignment-scope part of existing `Q-X3`.

**Status 2026-11-15:** Section A is decided or closed. Section B has one open
client gate (Q-T6C); the rest are resolved or superseded by the Types 1-4 and
assignment gaps. Two items in the Open Gaps table still wait for evidence: G-17
class D (extract SP-01) and G-19 parity (extract SP-03). See
[Consistency Review](#consistency-review-2026-11-15) and
[Required Migration Patterns](#required-migration-patterns-for-unified-workflow).

**Reconciled 2026-11-16:** the [canonical contract](../../../specs/001-data-migration-jobs/contracts/ss-mapping-contracts.md)
now incorporates the final decisions. Q-T6C was approved 2026-11-15, not still open.
Current request authorizes migration-only implementation; production writes remain gated.
The [001 plan](../../../specs/001-data-migration-jobs/plan.md) resolves EN engineering choices;
all follow-ups are in the [001 backlog](../../../specs/001-data-migration-jobs/backlog.md).
Fixed cutover `2026-12-01` supersedes run-date boundaries, including retained leave-type expiry.

## Settled Rules: Do Not Reopen

- Durable source-ID crosswalks, generated target IDs, Unified audits, read-only SS,
  unchanged existing references, and Q-X4 replay/conflict protection still apply.
- Type 5: seven approved snapshot links to five standard codes; 37 distinct retained
  expired paid types. SPL Parental Leave, All Day Medical, and Illness are retained
  expired. Only LWOP is unpaid. Approved Code trimming, insert dates, and Description
  fallback/truncation stay settled. No Code truncation is approved.
- Type 6: exact untrimmed Code, null-only LocationId/SubCode, positive-notice rules,
  append Order, and course/certificate expiry copying are approved. Do not recalculate expiry.
- Type 7 matching, defaults, and append order are closed. Its scope exceptions do
  not automatically apply to other types.
- Q-P1 through Q-P6, Q-A1 through Q-A3, Q-E1/Q-E2, and the approved parts of Q-X1
  through Q-X4 stay settled. New away, leave, and shift events use AllDay=false
  with source times/timezone unchanged.
- T051's JC protection policy is settled. Its implementation and validation remain gates.

### Recent Training Decisions

| Question Code | SS Source | Unified Target | Short Description of need | Rule Proposed | Decision |
| --- | --- | --- | --- | --- | --- |
| Q-P7 | Type 6 training courses | Trainings and TrainingProfileRequirements | Preserve the course catalogue and scope mandatory Carbine courses to the Carbine profile. | Transfer all Type 6 courses, expired or not. Link the four mandatory Carbine courses to `CARBINE_OPERATOR`: `Carbine Initial Course`, `Carbine Practice 1`, `Carbine Practice 2`, and `Carbine Annual Recertification`. Do not link `Carbine Selection` or `Carbine Operator Course`. No other course group gets profile links. | Approved 2026-11-10. Carbine is the only profile-scoped group; Q-P5 governs user assignments. |
| Q-P5 | SheriffTraining history | Users.TrainingProfileId | Identify migrated users who receive the Carbine profile. | Assign `CARBINE_OPERATOR` to migrated users with at least one non-deleted Carbine training record within the Q-X3 history cutoff; leave other users unassigned. Preserve existing non-null target profiles. | Approved 2026-11-12: option B, subject to the migration cutoff. Carbine history ending before the cutoff does not qualify a user. |
| Q-X2 (training) | SheriffTraining.ExpiryDate and ExpiryReason | No deleted-record marker on UserTrainings | Distinguish a deleted individual training record from an expired course or certificate. | Skip and count deleted individual training records; do not create a new target or crosswalk for them. If a previously migrated record is later deleted in SS, retain its target and crosswalk and log a warning. Copy certificate expiry exactly, including null; transfer expired courses. | Confirmed 2026-11-12: ignore deleted individual records. No Unified inactive-record feature is required now. A separate future migration is optional if that feature is added later. |
| Q-E9 (training) | SheriffTraining of sheriffs with Sheriff.Excused = true | None (not transferred) | Keep excused sheriffs' training out of Unified. | Skip and count every training record whose sheriff has `Excused = true`, regardless of `IsEnabled`. Null/false transfers normally. No target or crosswalk. Sheriff user still migrates. Skipped records do not qualify Q-P5. If a sheriff becomes excused after migration, retain targets and log a warning. | Approved 2026-11-12. Exception to Q-E9 option B; the findings log and future report-grouping backlog stay. |
| Q-T6R | Type 6 `Rotating`, `ValidityPeriod`, eligible SheriffTraining history | `Trainings.Rotating` | SS uses this flag for expiry/status/notice; Unified uses it to allow renewal and set notice wording. | For new courses, set target true if the source flag is true, `ValidityPeriod > 0`, or eligible history has repeated completions for a sheriff/course. Otherwise false. Existing target courses stay unchanged. Does not change expiry calculation or migrate SS status logic. | Approved 2026-11-12 (option B). Add Unified backlog item: review SS's extra year before "alert" with Josh before any implementation. |
- **Q-P5 cutoff confirmed 2026-11-12:** only non-deleted Carbine history with
  `EndDate` on or after the configured Q-X3 cutoff qualifies. The initial inclusive
  cutoff is `2015-01-01T08:00:00Z`; earlier history does not qualify.
- **Q-E6 follow-up:** the approved start/end mapping is in Section A. The
  `AwardedOn`/`EndingOn` expiry-base inconsistency and field renaming are
  out-of-scope application fixes for later; they do not authorize app changes now.

## A. Mapping Choices Ready For Review

Common rule: preserve existing targets and valid crosswalks. A proposed exception
does not authorize deleting a prior link or overwriting its target.

| Question Code | SS Source                                                               | Unified Target                                                       | Short Description of need                                                                                                                                       | Rule Proposed                                                                                                                                                                                                                                                                                                                                                           | Decision                                                                                                         |
| ------------- | ----------------------------------------------------------------------- | -------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| Q-L1 | Type 5 retained-type Code | LeaveTypes.Code | Guard invalid identity values after approved trimming. Source snapshot has no blank or overlength codes. | Require nonblank Code within the target max 50; no Code truncation. Invalid values raise a row exception with no insert/new link. | Approved 2026-11-15: keep the cheap exception guard (supersedes "ignore"). Snapshot is clean, so no rows expected. |
| Q-L2          | Seven approved standard-target snapshot links and three earlier aliases | Five standard LeaveTypes codes                                       | Deployed standard target may be missing or inconsistent with the approved mapping. Seeds are not deployed-data proof.                                           | Require a unique valid canonical target consistent with approved IsPaid. Missing, ambiguous, or conflicting targets raise exceptions and block dependent leave rows. Do not insert an alias or alter the target.                                                                                                                                                        | Unified Leave Types are the standardized by the seed, this should be treated as the deployed data.               |
| Q-L3          | Retained Type 5 Code after trimming                                     | LeaveTypes                                                           | Decide how an unexpected target-name or trimmed-source-name collision is handled. User asserts no target collisions; this is not independently verified.        | Resolve valid crosswalks first. Otherwise, a retained-name collision raises an exception; no automatic merge, overwrite, or new link. Explicit approval is needed to reuse a colliding target.                                                                                                                                                                          | I'm expecting no conflicts. Ignore this concern, and just raise a concern to ensure no silent failures           |
| Q-L4 | Type 5 LocationId and SubCode | No corresponding fields | Source scope is still unapproved. All 44 snapshot rows have both fields null. | Require both null. Any non-null value, including empty/blank SubCode, raises an exception with no new target/link. This is a separate Type 5 choice, not inherited from Type 6/7. | Approved 2026-11-15: keep the cheap exception guard (supersedes "ignore"). All 44 snapshot rows are null, so no rows expected. |
| Q-L5          | Type 5 LookupSortOrders                                                 | No order field on LeaveTypes                                         | Leave types sort by Code in target service code. Source ordering has no matching target field.                                                                  | Omit source leave-type sort rows; report exclusion counts in reconciliation. Do not add a target order field.                                                                                                                                                                                                                                                           | Safe to ignore sort order not required in new code base.                                                         |
| Q-L6          | Type 5 codes outside the accepted snapshot and earlier approved aliases | LeaveTypes                                                           | SS end-of-life does not make an unexpected code safe to infer. Earlier user-supplied labels are not proof those codes exist in production.                      | Raise an exception; no inferred target or IsPaid, and no insert. Block dependent leave rows. Unobserved earlier-list labels do not gain mappings from similar names.                                                                                                                                                                                                    | As I'm not expecting any questionable inference, this should be safe to raise an exception                       |
| Q-P8          | Sheriff.Gender null                                                     | Users.Gender (non-nullable enum)                                     | EX-21: 3 Sheriff rows (1 enabled) have null Gender. D-05 covers name-to-name mapping only; the target has no null value and the CLR default is Male.            | Raise a row exception; no user insert. Alternative: insert `Other` with a logged finding. Never rely on the enum default.                                                                                                                                                                                                                                               | Set as Other, do not default to male                                                                             |
| Q-E3          | SheriffLeave.LeaveTypeId                                                | LeaveEntries.LeaveTypeId                                             | Source FK is nullable; target requires a type. EX-26 has no nulls in the supplied snapshot.                                                                     | Null raises an exception; no fallback leave type. A missing/invalid resolved type also blocks the row.                                                                                                                                                                                                                                                                  | Exception on null is fine, the data points to consistent application of a FK and thus should be no issue.        |
| Q-E4          | No source event title                                                   | Events.Title for leave                                               | Choose a title. Target UI code uses a staff-name leave title.                                                                                                   | Follow the UI convention: "{FirstName LastName} leave"; use "Leave" if no usable name. Names-free alternative: always "Leave".                                                                                                                                                                                                                                          | Happy with the recommendation to follow the UI convention.                                                       |
| Q-E5          | SheriffTraining.TrainingTypeId                                          | UserTrainings.TrainingId                                             | Source FK is nullable; target requires a course. EX-28 has no nulls in the supplied snapshot.                                                                   | Null raises an exception; no fallback course. A missing/invalid course crosswalk also blocks the row.                                                                                                                                                                                                                                                                   | Accept the proposed rule.                                                                                        |
| Q-E6          | SheriffTraining.StartDate and EndDate                                   | UserTrainings.AwardedOn and EndingOn                                 | Confirm field mapping and reconcile equal dates with generic X-03 rejection. Snapshot training includes equal start/end dates; target mapping permits equality. | Copy StartDate to AwardedOn and EndDate to EndingOn. For training only, permit equality and reject EndDate earlier than StartDate. Do not change other tables' range rules.                                                                                                                                                                                             | Accept the proposed rule. The logical error and renaming should be listed as a out of scope fix to perform later |
| Q-E7          | SheriffTraining.Note and inherited Comment                              | UserTrainings.Notes                                                  | `Comment` names the course when Type 6 Code is `Other`; `Note` is free text. Unified has one Notes field.                                                       | For `Other`, write `Other training: {Comment}`; if Note is also present and differs, append newline + Note. If equal, write the labeled Comment once. If Comment is absent, copy Note. Preserve source text without migration trimming. A Comment on a non-Other course raises an exception.                                                                            | Approved: use the revised proposal.                                                                              |
| Q-E8          | SheriffStandardTraining                                                 | Unified `Training.Mandatory`, profile requirements, and user-creation auto-assignment | SS has 12 standard-training rules with fixed calendar expiry dates and one conditional rule; Unified auto-assigns active mandatory courses using profile applicability and `ValidityDays`. | Do not migrate `SheriffStandardTraining` or produce a separate report. Use Unified mandatory flags, profile links, and auto-assignment for future users. Accept that the course set and expiry behavior differ from SS: Unified uses `ValidityDays` from creation time rather than SS's calendar rules. `Fraud Awareness and Prevention` remains non-mandatory unless changed in Unified after cutover. Historical SS training rows still migrate under their own rules; Q-P6 means migration does not trigger auto-assignment. | Approved 2026-11-12: accept the proposed replacement; no `SheriffStandardTraining` migration or report. |
| Q-E9          | Sheriff.Excused                                                         | No direct field                                                      | SS moves a sheriff's training rows to a separate Excused report table; it does not remove missing/expired status. Unified has no equivalent.                   | Omit the field; do not infer an exemption, profile, or role. Log the count and source IDs for `Excused = true` in restricted migration findings for manual review. Record a future Unified feature request for an equivalent training-report grouping; no app work is authorized now.                            | Approved 2026-11-12: option B. Omit and log findings; track a future Unified report-exclusion feature. Exception added 2026-11-12: do not transfer any training records of sheriffs with `Excused = true`; skip and count them. The sheriff still migrates; skipped records do not qualify Q-P5 Carbine. |
| Q-E10         | SheriffTraining.FirstNotice                                             | UserTrainings.NoticeState                                            | Source code sets true after sending the expiry notice.                                                                                                          | false to None; true to Sent; never Pending. Validate later that migration creates no forced notice-send side effect.                                                                                                                                                                                                                                                    | Approved 2026-11-12: false -> `None`, true -> `Sent`; never `Pending`. Keeps the "already notified" meaning. |
| Q-E11         | Repeat SheriffTraining rows for one sheriff/course                      | UserTrainings identity and Version                                   | SS has no version. EX-28 shows repeated date combinations, so dates alone are not a safe identity.                                                              | Keep each eligible source row distinct, resolved by source-ID crosswalk. Group by resolved user/course; order EndDate, StartDate, source ID ascending. Assign versions after the existing maximum (1 for an empty group); never renumber. A later older row gets max+1 and an out-of-order finding. Use controlled write path EN-6. | Approved 2026-11-12: accept this ordering. Eligible rows follow Q-X2, Q-E9, and Q-X3. |
| Q-R2          | Duty.AssignmentId                                                       | AssignmentEntries.AssignmentDefinitionId                             | Source FK is nullable; target requires a definition. Snapshot has no nulls. Full definition mapping still depends on Types 1-4.                                 | Null raises an exception; no placeholder definition. A missing/invalid definition crosswalk also blocks the row.                                                                                                                                                                                                                                                        | Closed 2026-11-15 by G-12a (stronger rule): keep the exception guard. |
| Q-R3          | DutySlot rows for each Duty                                             | AssignmentEntries.Capacity                                           | Decide whether flagged/unfilled slots count. Target entry validation allows zero; definition capacity is a separate question.                                   | Count all non-soft-deleted slots, including flagged/unfilled slots; allow zero. Unfilled slots contribute capacity, not invented staff/shift links. Alternative: exclude IsNotRequired slots. Read with Q-R5.                                                                                                                                                           | Superseded 2026-11-15 by G-12c/G-12e: capacity is 1 per entry (0 if Closed/NotRequired); slots are not counted. The proposed rule is withdrawn. |
| Q-R5          | DutySlot.IsNotRequired, IsNotAvailable, IsOvertime, IsClosed            | No corresponding fields                                              | Flag semantics would otherwise be lost, including overtime on filled slots.                                                                                     | Omit only with explicit approval; report per-flag counts. Retain staffing capacity per Q-R3 and real links per Q-R4. If flags must remain operational, approve separate target design rather than encode them into unrelated fields.                                                                                                                                    | Superseded 2026-11-15 by G-12c/G-12e and G-13a: Closed/NotRequired -> capacity 0; NotAvailable -> capacity 1, no user; IsOvertime per G-13b/G-19. Per-flag counts reported. |
| Q-R6          | Shift.OvertimeHours                                                     | No corresponding field                                               | EX-35 includes shifts with positive overtime hours. This is a real omission, not just a null-column cleanup.                                                    | Do not silently drop. Choose explicit report-only omission or separate target support. No safe business default is supported by code alone.                                                                                                                                                                                                                             | Superseded 2026-11-15 by G-13b + G-17/G-18/G-19: no stored field; Unified recalculates overtime from merged shifts and lunch values; parity is a tracked migration need. |
| Q-R7          | Shift.AnticipatedAssignmentId                                           | No direct field                                                      | No populated values occur in EX-35; future unexpected values still need a policy.                                                                               | Omit the field for null values. Any non-null value raises a review finding; do not infer a dated assignment link.                                                                                                                                                                                                                                                       | Closed 2026-11-15 by G-13d: nothing to migrate; a non-null value raises a finding. |
| Q-R8          | No source event title                                                   | Events.Title for shift                                               | Choose a title. Target UI code uses an employee-name shift title.                                                                                               | Follow the UI convention: "{employeeName} shift"; use a resolved name/IdirName, with "Shift" if neither is usable. Names-free alternative: always "Shift".                                                                                                                                                                                                              | Closed 2026-11-15 by consistency with approved Q-E4 (leave uses the UI name convention): "{employeeName} shift", fallback "Shift". |

## B. Parked Or Client-Gated Mapping Choices

The Q-R4 hold was lifted 2026-11-15: the AG-13C live extract settled it as G-13c.
Q-P7 was resolved and is recorded under Recent Training Decisions. Only Q-T6C
(client gate) is still open in this section.
Types 1-4 bookmark lifted 2026-11-13: two-step plan approved; open gaps are listed in
[Types 1-4 And Assignments: Open Gaps](#types-1-4-and-assignments-open-gaps).

| Question Code | SS Source                                                 | Unified Target                                                      | Short Description of need                                                                                                                                                                                                                                    | Rule Proposed                                                                                                                                                                                                                                                                                                                                                                      | Decision |
| ------------- | --------------------------------------------------------- | ------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------- |
| Q-R1          | Assignment weekday flags and AdhocStartDate/EndDate       | AssignmentDefinitions, AssignmentEntries; possibly AssignmentSeries | BLOCKED by Types 1-4/Stats. Choose recurring-series semantics versus dated duties only.                                                                                                                                                                      | Keep parked. Candidate: definitions from Assignment, dated entries from Duty, no synthesized recurrence series. Do not apply until definition/Stats mapping is settled.                                                                                                                                                                                                            | Superseded 2026-11-15 by G-9a/G-9b (windows), G-10 (qualifying current-to-future series) and G-11 (scope). |
| Q-R4          | Filled DutySlot and candidate Shift rows                  | ShiftAssignmentEntries                                              | HOLD - requires live data for validation; also blocked by Types 1-4. EX-34 has 63,922 uniquely containing cases and 1,335 other cases requiring review. Separately, EX-23 has 6 DutySlot rows ending before start (reject under X-03) and 1 equal start/end. | Keep hold. Candidate only: link a slot to exactly one containing shift for the same sheriff/location; exceptions for multiple, partial-overlap, or no-overlap matches. Inspect edge cases before approval.                                                                                                                                                                         | Hold lifted 2026-11-15: superseded by G-13c (link rules) with G-17 (shift merge). AG-13C is the live evidence. |
| Q-X3b         | Assignment lifecycle and references from in-scope Duty    | AssignmentDefinitions                                               | BLOCKED. Assignment has no EndDate for the approved Q-X3 cutoff predicate.                                                                                                                                                                                   | Keep parked. Candidate: include unexpired assignments and every assignment referenced by an in-scope duty; never exclude a definition needed by migrated history. Expiry status alone never excludes a definition (Q-X2 approved: expired assignments migrate with ExpiryDateUtc); only the history-scope predicate is open. Do not reopen the approved 2015-01-01 initial cutoff. | Superseded 2026-11-14 by G-11. |
| Q-R9          | No source publish state on Shift/Duty                     | Events.StatusTypeCode for shift and assignment events               | BLOCKED with roster. Unified edits only Draft and publishes Draft to Active; conflict checks ignore Draft. SS has no publish state. Away/leave status is already decided; shift/duty status is not.                                                          | Candidate: unexpired rows become `active` (treated as published); expired rows become `cancelled` under Q-X2. No `draft` without explicit approval.                                                                                                                                                                                                                                | Resolved 2026-11-14 by G-12b: after migration date -> Draft; on/before -> Active; expired -> Cancelled. |
| Q-R10         | No source event title for Duty                            | Events.Title for assignment events                                  | BLOCKED with roster. AssignmentEntry has an EventId; Q-R8 covers shift titles only.                                                                                                                                                                          | Candidate: use the resolved assignment-definition name; fall back to "Assignment". Settle after Types 1-4 definitions.                                                                                                                                                                                                                                                             | Superseded 2026-11-14 by G-12d: definition name. |
| Q-T6C         | Type 6 Category text                                      | TrainingCategories and new Trainings category link                  | CLIENT GATE. Category design is complete but not client-approved; an authoritative replacement catalogue may be supplied.                                                                                                                                    | Seek client approval of the existing design: blank means no category; exact-name reuse or valid category insert with null Description/generated ID; link newly inserted courses only. Do not replace/invent a catalogue.                                                                                                                                                           | Approved 2026-11-15 (client gate cleared): use the three SS labels as Unified TrainingCategories ("Provincial Standard" 22 courses, "Branch" 4, "Local" 4); blank (42) stays uncategorised. Exact-name reuse, else insert with null Description and generated ID; link newly inserted courses only. |
| Q-T1          | Type 1 CourtRole lookup and related Assignment data       | AssignmentDefinitions / Stats classification, target choice pending | BLOCKED. Decide target semantics, identity/normalization, location/global scope, required category/capacity/time/lifecycle fields, SubCode, and order.                                                                                                       | Keep bookmarked for a clean assignment-to-Stats review after seeds settle. Use source-ID crosswalks; do not treat a lookup label alone as a complete assignment definition. No insert defaults are approved here.                                                                                                                                                                  | Superseded 2026-11-13 by the Types 1-4 two-step plan; see Open Gaps. |
| Q-T2          | Type 2 JailRole lookup and related Assignment data        | AssignmentDefinitions / Stats classification, target choice pending | BLOCKED. Same field decisions as Q-T1, plus same-name identity across role types.                                                                                                                                                                            | Keep bookmarked. Settle target/Stats meaning and cross-type collision policy before defining matches or inserts; do not merge on name alone.                                                                                                                                                                                                                                       | Superseded 2026-11-13 by the Types 1-4 two-step plan; see Open Gaps. |
| Q-T3          | Type 3 EscortRun lookup and related Assignment data       | AssignmentDefinitions / Stats classification, target choice pending | BLOCKED. Resolve run-specific meaning, target identity, location/global scope, required fields, SubCode, and order.                                                                                                                                          | Keep bookmarked. Preserve source distinctions until the assignment-to-Stats model and seed catalogue are reviewed. No inferred run/category/time defaults.                                                                                                                                                                                                                         | Superseded 2026-11-13 by the Types 1-4 two-step plan; see Open Gaps. |
| Q-T4          | Type 4 OtherAssignment lookup and related Assignment data | AssignmentDefinitions / Stats classification, target choice pending | BLOCKED. Resolve catch-all category meaning, target identity, location/global scope, required fields, SubCode, and order.                                                                                                                                    | Keep bookmarked. Review alongside Types 1-3 and roster definitions; no generic fallback target or inferred field defaults.                                                                                                                                                                                                                                                         | Superseded 2026-11-13 by the Types 1-4 two-step plan; see Open Gaps. |
| Q-S1          | LookupSortOrders across lookup types                      | Per-type target ordering, not a copied table                        | BLOCKED close-out. Source linkage, local/global precedence, ties, and omissions still need resolution for Types 1-4.                                                                                                                                         | Candidate: do not copy this table wholesale; use explicitly approved per-type order contracts and reconcile omissions. Preserve approved Type 6/7 append rules and Type 0 no-reorder rule. Type 5 omission is Q-L5.                                                                                                                                                                | Closed 2026-11-15: G-14 (Types 1-4 not migrated), Q-L5 (Type 5 omitted), Type 6/7 append rules, Type 0 no-reorder. Nothing copied from LookupSortOrder. |

### Types 1-4 And Assignments: Open Gaps

Plan approved 2026-11-13: Step 1 builds a (Type, Code) -> SubCategory/Excluded crosswalk;
Step 2 migrates 2a Assignment, 2b Duty, then 2c DutySlot. See the
[canonical contract](../../../specs/001-data-migration-jobs/contracts/ss-mapping-contracts.md#types-1-4-and-assignments-approved-two-step-plan).
Evidence: EX-10, EX-31, and [lookup-extracts.md](./lookup-extracts.md). Counts are SS
assignments (live).

Revised 2026-11-13 for workflow continuity; 2026-11-14: Unified is the canonical scheduling method, so SS-only behaviour is not reproduced.
 Stats categories need only be close enough
(fixable later); definitions and entries must reproduce SS scheduling behaviour in Unified.

| Question Code | Step | Gap | SS today | Unified today | Recommended decision | Decision |
| --- | --- | --- | --- | --- | --- | --- |
| G-1 | 1 | F1 Documents codes (40) | No civil/criminal concept | Category drives calendar row grouping only | C5/22 Documents. Fix manually later if needed. | Approved 2026-11-14 |
| G-2 | 1 | Name override | Classification from code only | Category per definition | Code-only. No overrides. | Approved 2026-11-14: code only |
| G-3 | 1 | F5 Leave Waitlist, F7 Test | Ordinary codes; Waitlist ~650 duties, 1 filled | Leave has its own module | Exclude both; report counts and IDs. | Approved 2026-11-15: exclude both; report what is left behind (counts, IDs). Unified backlog item: consider a draft/waitlist leave-request feature. |
| G-4 | 1 | F2-F6 | Free-text codes | Any valid SubCategory | Approve as listed. | Approved 2026-11-14 |
| G-5 | 1 | Loan-out codes | Real loans are `SheriffAwayLocation` | Loan Out module | Codes stay assignments (C12/44). Real loans follow Wave 3. | Approved 2026-11-14 |
| G-6 | 1 | Type 0 CourtRoom (1,015) | Same `LookupCodeId` | No assignment-room link | C1/3; room kept in definition name. | Approved 2026-11-14 |
| G-7 | 1 | Supervision | None | Group not stored | Keep D3 in the frozen table. | Approved 2026-11-15 (original plan): fixed in the reviewed table; sergeant codes -> Supervision group; Inspector/Staff Sergeant/Supervisor -> C37/135; never group 3. |
| G-8 | 2a | Definition name | Optional, not unique, shown `code (name)` | Required <=100, unique per location (trim + upper) | Revised 2026-11-15 from AD-01..AD-04: merge by (location, lookup type, upper(trim(proposed name))) into one definition; crosswalk is many SS assignments -> one definition. 278 groups / 1,978 assignments. Name casing from the live member (else latest). Same name across types (10 groups, 0 future duties) stays split with a type suffix. Defaults: most common duty times; entries keep their own. Valid-to open if any member is live, else latest member valid-to (G-9b). 48 merge-induced same-sheriff dates are all past: insert and report (see G-16). | Approved 2026-11-15: merge. Merged groups use the most common duty times as defaults (overrides G-9d copy for groups of 2+). |
| G-9a | 2a | EffectiveDateUtc | None; ad hoc assignments show only from AdhocStartDate | Required; pickers hide definitions outside the window | AdhocStartDate if set, else `1900-01-01T00:00:00Z`; pulled back to the earliest migrated duty date if later. | Approved 2026-11-15: EffectiveDateUtc 1900-01-01T00:00:00Z for every migrated assignment, live or expired. Expired SS assignments become expired definitions; valid-to (G-9b) is the critical field. |
| G-9b | 2a | ExpiryDateUtc | ExpiryDate exclusive; AdhocEndDate inclusive | Exclusive | Earliest of ExpiryDate and AdhocEndDate + 1 day, as midnight UTC. Widened to cover migrated non-cancelled entries; widenings reported. | Approved 2026-11-15. |
| G-9c | 2a | DefaultCapacity | A duty is one position; extra staff = extra duties (FTE) | >=1 | 1. | Approved 2026-11-14 |
| G-9d | 2a | Default times | Start < End | Optional; End > Start | Copy. | Approved 2026-11-14. Clarified 2026-11-15: single-member definitions copy; merged groups use G-8 (most common duty times). |
| G-9e | 2a | Colour | Derived from type | Definition optional; event required | Fixed per type (CourtRoom/CourtRole #189fd4, Jail #A22BB9, Escort #ffb007, Other #7a4528); entries copy it. | Approved 2026-11-14 |
| G-10 | 2a | Weekday flags / ad hoc window | Drive manual "Populate"; duties are the schedule | Recurrence = RRULE series built by schedulers | Revised 2026-11-15 from AG-10/AG-10B: 43 live assignments hold 5,428 future duties; 5,424 match the weekday/time pattern; 41 run 26 weeks at 99-100% coverage (Populate output). Build one Draft series per such assignment only where every RRULE occurrence has a matching duty; link those entries. Extra FTE duties (509 dates) and off-pattern duties stay standalone. History stays standalone. Needs Unified-uops-lead check that series + EventSeries + entries can be inserted without regeneration. | Approved 2026-11-15 (corrected 2026-11-15). **Scope:** current-to-future records only = local dates on or after the migration date (inclusive). **Qualification:** one series per SS assignment only when every RRULE occurrence from the series start to its last occurrence has a matching live SS duty (flagged weekday, Assignment Start/End times, inside the ad hoc window). Any missing occurrence -> no series for that assignment. **Standalone:** non-qualifying duties stay standalone entries; this includes extra-staffing FTE duties (509 dates), off-pattern duties, duties of non-qualifying assignments, and all history. **Gap:** clean break at the migration date. No series starts before it, and no entry dated before it links to a series; history = standalone Active (or Cancelled) entries. **Status:** each occurrence follows G-12b (migration date -> Active; later -> Draft). If SS-V052 shows Unified cannot hold an Active occurrence in a series, start the series on the next local date and keep the migration-date entry standalone Active. Unified-uops-lead insert check remains (SS-V052). |
| G-11 | 2a | History scope (Q-X3b) | Expired assignments still on past rosters | Existing entries keep expired definitions | Migrate unexpired assignments plus any assignment used by an in-scope duty. G-9a/b windows cover their duties. | Approved 2026-11-14 |
| G-12a | 2b | Null `Duty.AssignmentId` | UI never creates | Required | Exception (Q-R2). | Approved 2026-11-15 (stronger rule): keep the exception as a guard even though the snapshot has 0; never repair or skip silently. |
| G-12b | 2b | Status (Q-R9) | No draft state | Draft is editable/unpublished; Active is published | APPROVED 2026-11-14 (user rule): entries dated after the migration date (exclusive) -> Draft; entries on or before the migration date -> Active. Expired source rows -> Cancelled (Q-X2). Date is the entry's local start date in the location timezone. Also applies to migrated shifts. | Approved 2026-11-14 |
| G-12c | 2b | Duty with 0-1 live slot (~97.5% of live duties) | Duty = one position; slot = who covers it | Entry capacity = headcount; partial cover shown natively | One entry per duty, duty times, capacity 1. Capacity 0 if the slot is Closed or NotRequired. NotAvailable: capacity 1, no user. | Approved 2026-11-15. |
| G-12d | 2b | Event title (Q-R10) | `code (name)` | Title | Definition name. | Approved 2026-11-14 |
| G-12e | 2b | Duty with 2+ live slots (2,338) | Sequential time segments of one position | 2 users on capacity 1 show "capacity exceeded" | Revised 2026-11-15 from AG-12: one entry per duty per distinct sheriff, spanning that sheriff's slots, capacity 1. Same sheriff repeated (1,599 duties) -> one entry. Different sheriffs (518 duties, all past) -> one entry each, so no "capacity exceeded". Flag-only segments beside a sheriff (220 duties) are dropped and reported. A duty with no sheriff -> one entry per G-12c. | Approved 2026-11-15. |
| G-13a | 2c | Slot flags (Q-R5) | Slot with no sheriff | Capacity 0 shows "Closed" | Via G-12c/e; report per-flag counts. | Closed 2026-11-15 by G-12c/G-12e; per-flag counts reported. |
| G-13b | 2c | Overtime (Q-R6) | Slot flag plus calculated hours | Calculated at run time | Omit both; report counts. | Approved 2026-11-14 (omit stored fields). 2026-11-15 migration need: Unified must reproduce SS overtime from migrated data; see G-17/G-18/G-19 and SS-V051. |
| G-13c | 2c | Slot -> shift (Q-R4) | Time-overlap match | Explicit link | Data available 2026-11-15 (AG-13C). Users reach entries only via a ShiftAssignmentEntry link. 1a (63,975): link to that shift. 2a duplicate shifts (516): link to the shift the shift migration keeps, else the lowest source ID. 3a adjacent shifts (761): link to every overlapping shift. 3b partly outside (17): link overlapping shifts and report. 4 no shift (42): entry with no user; report. All non-1a slots are past. | Approved 2026-11-15. Class 4 (42 slots, 0.064% of filled, all past): drop the user link as bad data, keep the entry unfilled per G-12c, report IDs. Material break = >0.1% of filled slots or any future slot; report and stop for review. Classes 2a/3a resolve to the merged shift under G-17. |
| G-13d | 2c | AnticipatedAssignmentId (Q-R7) | Web never uses | None | Omit; a non-null value raises a finding. | Approved 2026-11-15: nothing to migrate. |
| G-14 | 1 | Ordering (Q-S1) | `LookupSortOrder` per location | Sorted by category, subcategory, name | Do not migrate. Raise a Unified backlog item for a definition display order. | Approved 2026-11-14 |
| G-15 | 2a/2b | Comments | `Assignment.Comment`, `Duty.Comment` | `AssignmentDefinition.Description`, `Event.Description` | Copy exactly. Check that the UI shows Event.Description (deferred). | Approved 2026-11-14 |
| G-16 | 2c | Same sheriff twice on one definition/date | Allowed when slots don't overlap | Blocked by `AssignmentUniquenessValidator` | Revised 2026-11-15 from AG-16: same-duty repeats (1,600 keys) are resolved by G-12e. FTE repeats (53 keys) and merge-induced repeats (48, G-8) are all past and read-only (Active); the uniqueness check is API-only (no DB index). Insert them and report; 0 future keys. | Approved 2026-11-15: insert past conflicts directly and report. |
| G-17 | Shift | Several shifts per sheriff, location and local date | Normal: SS `SplitLongShifts` cuts shifts at 8 h; overtime is added as extra shifts; duplicates exist (AG-13C 2a) | One shift per user, location and local start date. `ShiftConflictService` blocks create, update and publish (not overridable) | Revised 2026-11-15 from SP-01/SP-02. No-gap groups (A identical 705, B overlapping 8, C touching 3,289 incl. 15 future) merge: start = earliest, end = latest, comments joined, one crosswalk row per source shift. Gap groups (D): 694 past -> one Active shift per gap-free block, reported. 4 future (Surrey 2026, 8 shifts, gaps 135-250 min) -> exception listed for Surrey schedulers to fix in SS before cutover or rebuild in Unified. SP-02: 0 cross-date overlaps. Different locations stay separate. OD-01 (2026-11-16): the 34 gap days with overtime lose 45.8 h under the per-shift calculation (SS 50.9 h, Unified 5.1 h). Rejected alternative: merging across the gap keeps one shift per date but counts unworked gap time as worked (gap p50 90 min, max 330 min). | Recommended 2026-11-16 (final): approve the gap rule as written. Accept the 45.8 h display loss on 34 past days; report them (SS-V051). The calculation gap is backlog item BL-4. Approved 2026-11-16. |
| G-18 | Shift | Lunch | No lunch field; overtime threshold 8.0 h of raw shift time (`OvertimeHoursPerDay`) | `LunchAvailableMinutes` 0-60 and `WorkedLunchMinutes`; null lunch gives null overtime. API default 60 / 0 (`DefaultLunchMinutes`) | LunchAvailableMinutes = 60, WorkedLunchMinutes = 0 on every migrated shift. With the 420-minute threshold this equals the SS 8 h rule. | Closed 2026-11-15: required by the G-13b reclaim need; matches the API default. Confirm deployed `WorkingHours` options (SS-V051). |
| G-19 | Shift | Overtime parity (G-13b need) | Stored `Shift.OvertimeHours` per sheriff-day across locations | Calculated per shift and user: credited minutes - `FullWorkingDayMinutes` (420); adds assignment minutes outside the shift | Revised 2026-11-15 from SP-03. With the G-17 merge and G-18 lunch, future overtime matches exactly (2027: 30.0 vs 30.0 h). Past: SS 6,743.7 h vs Unified 6,355.4 h (-388.3 h, 94.2%) on 84 sheriff-days; largest in 2025 (-192.5 h). Cause split (2026-11-16, OD-01, supersedes the earlier SP-based estimate of "50+ cross-location days"): 34 same-location gap days (SS 50.9 h, Unified 5.1 h, -45.8 h; Daily-1 matches all 34); 1 cross-location overlap day (SS 8.0 h, Unified 0 h; SS counted the overlap twice, so 0 is correct); 49 one-shift days (-334.5 h; SS stored 334.5 h more than Unified). Signed values are Unified minus SS; -45.8 - 8.0 - 334.5 = -388.3. On those 49 days the per-shift and per-day rules agree, so no calculation change recovers them. SS stored more than its own rule gives for the merged shift; duplicate shift rows carrying stored overtime (713 one-shift days had duplicates) are the likely cause. SS recalculation is not a usable baseline (14,184.8 h vs 6,684.8 h stored) because it double counts duplicates. Without the G-17 merge, Unified finds ~0 h, so the merge is mandatory. Go-live impact: the future difference is 0 h (30.0 vs 30.0). In over 10 years of history there are 0 non-overlapping multi-location days with overtime. Code trace (2026-11-15): SS `CalculateOvertimeHoursForSheriffOnDay` sums gross hours of all the sheriff's shifts inside the local day across locations, minus 8.0 h, with no lunch deduction. Unified `WorkingHoursService` evaluates each shift+user alone: (minutes - lunch 60 + worked lunch) - 420. One shift: same result (8 h gross). Unified checks each shift alone, so only a single shift longer than 8 h earns overtime. On days with two or more shifts (gap or multi-location), the overtime that SS found by adding the shifts together is lost, unless one shift alone is over 8 h. This is a Unified gap beyond migration, but OD-01 shows it is rare: it applies only to multi-location days without overlap, and there are 0 such days in history. Same-location split shifts cannot be created in Unified after go-live. Candidate fix (Daily-1): per user and local day, take the union of all the user's shift time across locations (overlaps counted once), deduct one lunch, then subtract 420. The union never counts gap time, so it does not reopen the G-17 gap concern. It also never double counts: same-location duplicates are merged by G-17, and cross-location overlaps are allowed in Unified (`ShiftConflictService` checks per user and location only) but are counted once. Lunch per shift (Daily-2) still loses 1 h per extra shift. SS summed identical or overlapping shifts, so SS stored overtime on duplicate/overlap days is inflated; Daily-1 will differ there by design, and those days are reported, not reproduced. Evidence: [overtime-daily-extractor.sql](./overtime-daily-extractor.sql) -> [overtime-daily-extracts.md](./overtime-daily-extracts.md) (run 2026-11-16). | Recommended 2026-11-16 (final): (a) accept the -388.3 h past difference; list all 84 sheriff-days with their cause and SS stored hours in the findings report (SS-V051). (b) No Unified change before go-live; future parity is exact. (c) Log the per-day calculation (Daily-1) as backlog item BL-4, not as a serious go-live gap. Approved 2026-11-16: (a) and (b) accepted; BL-4 accepted; fix for the past difference is backlog item BL-6. Every one of the 84 days is a `MigratedWithDifference` finding in the final report. |

### Required Migration Patterns For Unified Workflow

Findings from Unified code (2026-11-15). Each item is a load pattern the job must follow so users
keep working at go-live. No code change is proposed; Unified is treated as feature complete.

1. **Shifts before links.** Users reach an assignment entry only through `ShiftAssignmentEntry`
   (ShiftEntryId + AssignmentEntryId) -> `ShiftAssignmentEntryUser`. Load merged shifts (G-17)
   first, then entries, then links (G-13c).
2. **One shift per user, location and local date** (G-17). Future Draft shifts that break this
   cannot be published.
3. **Many-to-one definition crosswalk** (G-8). Duty -> entry lookup goes through the
   assignment crosswalk, never through a name match at load time.
4. **Status split** (G-12b). Local date after the migration date -> Draft; on or before ->
   Active; expired -> Cancelled. Draft is invisible to other conflict checks and editable;
   Active is not editable through the API.
5. **History/current-to-future boundary** (G-10). Series cover only records dated on or after the
   migration date, and only for assignments where every occurrence has a matching SS duty.
   Non-qualifying duties (FTE extras, off-pattern, non-qualifying assignments) stay standalone.
   Nothing dated before the migration date links to a series. History is standalone Active or
   Cancelled entries.
6. **Go-live publish step (runbook).** Future migrated shifts and entries are Draft, so staff
   do not see them until a scheduler publishes per location and date range
   (`SchedulePublishService`). Publish is blocked by shift conflicts and by unresolved calendar
   conflicts (leave, training, holiday, other scheduling events); calendar conflicts can be
   overridden. Run SP-04/SP-05 before go-live, then the publish preview per location. Plan the
   publish before the first affected local date.
   SP-05 (2026-11-16): only Surrey Provincial Court holds future shifts (2,854 shifts, 58
   sheriffs, to 2027-05-07), so the go-live publish is a Surrey-only task. SP-04: 12 of them
   overlap SS away (11) or training (1) rows; these are overridable calendar conflicts.
7. **API-only rules.** Assignment uniqueness and shift conflicts are checked in services, not
   in the database. Past rows that break them are inserted directly and reported (G-16, G-17).
8. **Lunch and overtime** (G-18, G-19). Set lunch values on every shift; overtime is never
   stored.

### Consistency Review 2026-11-15

Checked all rows against later decisions. Later decisions win where they were made with live
extract evidence.

| Item | Inconsistency | Resolution |
| --- | --- | --- |
| Q-R3 | Proposed "count all slots as capacity" | Superseded by G-12c/e (AG-12 evidence). Closed. |
| Q-R1, Q-R4, Q-R5, Q-R6, Q-R7, Q-R10, Q-X3b, Q-S1, G-13a | Open or held, but answered by later G-rows | Closed or superseded in place. |
| Q-R8 | No decision | Closed by consistency with Q-E4. |
| G-9d vs G-8 | "Copy" vs "most common duty times" | Merged groups use G-8; single members copy. |
| G-13c 2a/3a | "Link to the kept shift" / "every overlapping shift" | Same-location cases become one merged shift (G-17); link to it. Cross-location cases keep one link per shift. |
| G-13b | "Omit, report" read as overtime loss | Omit the stored fields only. Overtime is reclaimed by G-17/G-18 and checked by G-19. |
| Q-L1, Q-L4 | Decision said "ignore"; rule raises an exception | Resolved 2026-11-15: user approved the exception guard. |
| Q-L2 vs Known Target-Code Interaction | Decision: seed = deployed data. Note: seeds are not proof | Code supports the decision: `LeaveTypeSeeder` resets the five codes at startup. Keep the guard exception for a missing target; treat the note as satisfied by startup seeding. |
| Contract | Q-L1-Q-L6, Q-P8, Q-E3-Q-E6, G-8 to G-19 not yet in the canonical contract | Follow-up outside this folder; not edited here. |

### Unified Backlog Items From Mapping Review

These are product follow-ups, not migration blockers. None needs a Unified change before go-live.

| ID | Source | Item | Evidence |
| --- | --- | --- | --- |
| BL-1 | Q-T6R | Review SS's extra year before a training "alert" with Josh before any implementation. | Training review |
| BL-2 | Q-E9 | Report grouping for excused sheriffs' skipped training. | Findings log |
| BL-3 | G-3 | Consider a draft or waitlist leave-request feature (replaces SS Leave Waitlist duties). | ~650 waitlist duties excluded |
| BL-4 | G-19 | **Accepted 2026-11-16.** Overtime per user and local day: take the union of all the user's shifts across locations (overlaps counted once), deduct one lunch per day, then subtract 420. Also decide whether one location needs split shifts (gap longer than lunch); today Unified allows one shift per user, location and date. Implementation detail: `WorkingHoursService` filters by `ShiftLocationIds` before it calculates. A per-day rule must load all the user's shifts for the day, calculate once, then attribute the result to the filtered locations. | OD-01: 34 past gap days (-45.8 h), 0 non-overlapping multi-location days with overtime, 0 future difference |
| BL-5 | G-14 | Display order for assignment definitions. | SS `LookupSortOrder` not migrated |
| BL-6 | G-19 | **Accepted 2026-11-16.** Historical overtime reconciliation, so Unified history totals match SS stored overtime. Details below. | OD-01: 84 past days, -388.3 h |

#### BL-6 Implementation Details (Historical Overtime Reconciliation)

Why: no calculation rule reproduces all of SS stored overtime. Unified computes overtime from
shifts; it stores no overtime value (`WorkingHoursService`, `WorkingHoursResult.OvertimeMinutes`).
The 84 past days split three ways:

| Part | Days | Hours (Unified minus SS) | Fix |
| --- | ---: | ---: | --- |
| Same-location gap days | 34 | -45.8 | BL-4 (Daily-1) recovers all 34 |
| Cross-location overlap | 1 | -8.0 | Adjustment record only; SS double counted |
| One merged shift, SS stored more | 49 | -334.5 | Adjustment record only; SS stored values from duplicate rows |

Steps (Unified-uops-lead when implementation is authorized):

1. **Model.** Add `LegacyOvertimeAdjustment` (Scheduling module):
   - `Id`, `UserId`, `LocalDate`, optional `LocationId`, signed `Minutes`, `ReasonCode`
     (`GapDay`, `CrossLocationOverlap`, `StoredExceedsRule`), `Source` = `SS`,
     `SourceShiftIds` (text) and Unified audit fields.
   - Unique `(UserId, LocalDate, Source)`.
   - Read-only in the UI and API.
2. **Migration write.** The Scheduling job writes one row per past sheriff-day where
   |SS stored - Unified computed| >= 0.01 h, after the shifts load.
   - Minutes = (SS stored - Unified computed) x 60, using the calculation deployed at go-live.
   - If BL-4 ships first, gap days drop out automatically.
   - Crosswalk: `EntityType = OvertimeAdjustment`, `LegacyId = sheriffId|localDate`.
   - Payload hash covers the source shift group.
   - Rollback deletes the row. Future days never get adjustments.
3. **Calculation.** `WorkingHoursResult` gains a `LegacyAdjustmentMinutes` field, filled per user and day.
   - `OvertimeMinutes` stays rule-calculated and unchanged.
   - Reports and exports show `OvertimeMinutes + LegacyAdjustmentMinutes` as "reported overtime" only when the query asks for legacy adjustments (`IncludeLegacyAdjustments`, default true for date ranges before the cutover date).
   - Location-filtered queries attribute the adjustment to its `LocationId`, or to the user's home location when null.
4. **Tests.**
   - For each of the 84 days, computed + adjustment = SS stored.
   - No adjustment on future dates.
   - Rollback removes all adjustments.
   - Extend SS-V051.
5. **Product-owner check before build.** The 49 + 1 day values are SS double counts. Confirm
   payroll wants them shown, not corrected. If not, skip step 2 for `StoredExceedsRule` and
   `CrossLocationOverlap`. Keep them only as report findings.

## C. Engineering Design Choices

These are not missing business mappings and do not authorize implementation.
EN-3 is intentionally unused.

| Question Code | SS Source                             | Unified Target                                 | Short Description of need                                                                                                                      | Rule Proposed                                                                                                                                                                                                         | Decision |
| ------------- | ------------------------------------- | ---------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------- |
| EN-1          | Tables governed by Q-X3               | Migration progress/state                       | Automatic backfill after an approved earlier cutoff is settled; persistence and resume design remain open.                                     | Persist applied cutoff and resumable backfill progress per source table. Replays use crosswalks and Q-X4; changing the cutoff must not depend only on ordinary incremental watermarks.                                |          |
| EN-2          | Type 6/7 insert order allocation      | Trainings.Order and PositionTypes.DisplayOrder | Approved append rules need serialization with ordinary application writers.                                                                    | Choose a transaction/locking scheme covering both migration and application writers. A migration-only advisory lock is insufficient. Verify later under SS-V045.                                                      |          |
| EN-4          | Rejected or blocked rows across waves | Migration outcomes, retry, and watermarks      | Define run continuation and retry so row failures cannot vanish behind an advanced watermark.                                                  | Continue independent rows; record source-keyed exceptions; retry rejected/blocked rows regardless of watermark. Only approved skip classes count as skips; never disguise a failure as success.                       |          |
| EN-5          | Per-table incremental payloads        | PayloadHash and read/replay boundaries         | Exact per-table fingerprint fields/encoding and boundary handling need a deterministic design.                                                 | Specify mapped-field lists and canonical null/date/enum encoding. Define stable paging/ties and replay overlap so Q-X4 comparisons and reconciliation are reproducible.                                               |          |
| EN-6          | In-scope SheriffTraining rows         | Tracked UserTrainings migration writes         | Normal renewal services may reject historical versions or recalculate expiry. Those behaviors must not defeat the approved migration contract. | Use a controlled tracked migration write path with audits, FK/unique checks, source-ID links, copied expiry including null, and the approved Q-E11 version policy. Do not invoke ordinary renewal/expiry calculation. |          |
| EN-7          | Build/test data for all three SS jobs | Disposable synthetic SS DB, then Unified        | Test end to end without production rows. A de-identified dump was withdrawn 2026-11-16: it needs heavy vetting.                               | Schema from SS EF migrations. Aggregate-only shape extract (SH). Deterministic synthetic generator with planted rule cases (`synthetic.case_manifest`). SS report services as oracle; Unified reports must match except for known findings. See [Synthetic SS Database And Report Parity](../migration-job-design.md#synthetic-ss-database-and-report-parity-required-2026-11-16-revised); checks SS-V056, SS-V057. | Directed 2026-11-16 (no dump; synthetic plus oracle). Generator and shape extract validated. Parity blocked: no Unified entity writers exist yet. Oracle harness and compare need implementation approval. |

## D. Deferred Checks And Approved Gates

No migration tests or writes were run. Read-only production extracts were run by the user
(see Evidence And Limits); no other database queries were run.

| Existing reference                   | Check or action still needed                                                                                                                                                                                                                                                    | Status                                                                         |
| ------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------ |
| SS-V015                              | Verify actual target-name collisions, retained-name uniqueness after trimming, and future-dated leave linked to retained expired types. Review editing behavior where an expired type is no longer available. EX-24 has 243 leave rows starting in 2027, but no type breakdown. | Deferred validation. User's no-collision assertion is not a target-data check. |
| SS-V046                              | Verify supplied Type 6 EffectiveDate timestamp semantics. EX-01 confirms timestamptz; EX-02 shows 70/70 Type 6 EffectiveDate null, so no snapshot course currently needs supplied-value interpretation.                                                                         | Deferred validation; do not reopen the approved null-date default.             |
| SS-V044                              | Confirm duplicate exact Type 6 Codes remain absent; if encountered, raise for separate review instead of inventing a merge rule.                                                                                                                                                | Deferred validation / exceptional-case review.                                 |
| Q-X1 read-path checks                | Confirm null-timezone fallback behavior in relevant target read paths.                                                                                                                                                                                                          | Deferred validation, not a new timezone mapping choice.                        |
| T051; SS-V036/038/039/048            | Apply approved JC protection, including CourtRoom default and write preflight; verify all three JC expiry flags are false in each target environment.                                                                                                                           | Approved implementation requirement; writes remain gated.                      |
| SS-V026/028/045                      | Validate incremental boundaries, fingerprints, replay, and order-writer serialization after implementation is authorized.                                                                                                                                                       | Deferred validation.                                                           |
| Lookup worksheet Type 6              | Its "Location/SubCode variants" row and status header lag the 2026-11-09 null-only approval.                                                                                                                                                                                    | Doc reconciliation only; do not reopen.                                        |
| Q-E10 / training notification checks | Confirm copied NoticeState and expiry do not cause an unintended migration-triggered notice send.                                                                                                                                                                               | Deferred validation; not permission to send notices.                           |

The routine D-01 through D-13 walkthrough remains in the
[later-wave review](./people-events-roster-review.md). D-07 is checked; other
unchecked boxes are not automatically new business decisions. Reconcile them with
approved Q-P/Q-A/Q-E/Q-X rules and the choices above instead of reopening settled
overlaps. Remaining field-for-field and exclusion sign-off is still required where
the current review genuinely leaves it pending.

### Known Target-Code Interaction

The target LeaveType seeder resets its five standard codes, including lifecycle and
paid/description defaults. None of the 37 retained names is one of those standard
codes. This is a target startup-code interaction, not permission for migration to
modify existing targets. Include it in collision/replay validation; do not infer
that a seed file proves the deployed target's state.

## Evidence And Limits

- **Verified source snapshot:** EX-* references mean the user-generated read-only
  production extract dated 2026-11-09 in [mapping-extracts.md](./mapping-extracts.md).
  They are snapshot evidence, not a new live query. Source "live" counts mean
  non-soft-deleted rows, not necessarily future or ongoing events.
- **User assertions:** no target-name collisions; SS will reach end-of-life after
  cutover and future codes are not expected. These do not replace validation.
- **Seeds and UI:** target seeds show checked-in defaults; UI code shows title
  conventions. Neither proves deployed target data or historical user intent.
- Type 6 categories remain client-gated. Q-R4 is settled by G-13c; Q-P7 is approved. Types 1-4
  follow the approved two-step plan. No parked item becomes approved merely by appearing in this worksheet.
- **Live read-only extracts (2026-11-14/15):** [lookup-extracts.md](./lookup-extracts.md),
  [assignment-gap-extractor.md](./assignment-gap-extractor.md) (AG-*, output of
  `assignment-gap-extractor.sql`), and [assignment-dedup-extracts.md](./assignment-dedup-extracts.md)
  (AD-*). Pending: `shift-publish-extractor.sql` (SP-*).
- Version allocation explicitly preserves existing target versions. A new empty
  user/course group may start at 1; an existing group must not restart at 1.

## Review Record

- Independent read-only review ran on the explicitly requested **Claude Fable 5.1**
  model (rubber-duck agent `fable-worksheet-review`). It verified the EX-* counts, UI
  title code, Type 6 approval status, row counts, and empty Decision cells. It found
  no must-fix issues.
- Applied from that review: added Q-P8 (null Gender), Q-R9 (shift/duty event status),
  Q-R10 (duty title); Q-X3b now defers to Q-X2; EX-23 slot range note on Q-R4;
  SS-V046 evidence; lookup-worksheet lag note; EN-3 gap note.

### Working Records

- [Canonical SS contract](../../../specs/001-data-migration-jobs/contracts/ss-mapping-contracts.md)
- [Lookup worksheet](../../../specs/001-data-migration-jobs/contracts/lookup-mapping-clarifications.md)
- [Tasks and gates](../../../specs/001-data-migration-jobs/tasks.md)
- [Leave decisions and accepted snapshot](./leave-mapping-review.md)
- [Training field review and client gate](./training-field-review.md)
- [Rank review: matching closed](./rank-mapping-review.md)
- [People, events, roster questions and routine walkthrough](./people-events-roster-review.md)
- [Deferred validation register](./validation-tests.md)
- [Production extract evidence](./mapping-extracts.md)

### Code Evidence Used By The Review

- Leave target constraints/defaults:
  [LeaveTypeConfiguration](../../../unified-scheduling/db/Configuration/LeaveTypeConfiguration.cs),
  [LeaveTypeConstants](../../../unified-scheduling/api/Unified.Leave/Constants/LeaveTypeConstants.cs),
  [LeaveTypeSeeder](../../../unified-scheduling/api/Unified.Leave/Seeders/LeaveTypeSeeder.cs),
  [LeaveTypeService](../../../unified-scheduling/api/Unified.Leave/Services/LeaveTypeService.cs),
  and [LeaveForm](../../../unified-scheduling/web/src/modules/leave/components/LeaveForm.vue).
- UI title conventions:
  [leaveRequest](../../../unified-scheduling/web/src/modules/leave/leaveRequest.ts)
  and [calendarSchedulingShiftForm](../../../unified-scheduling/web/src/modules/scheduling/calendarSchedulingShiftForm.ts).
- Training identity, dates, and notice state:
  [UserTraining](../../../unified-scheduling/db/Models/Training/UserTraining.cs),
  [UserTrainingService](../../../unified-scheduling/api/Unified.Training/Services/UserTrainingService.cs),
  [UserTrainingMappings](../../../unified-scheduling/api/Unified.Training/Mappings/UserTrainingMappings.cs),
  and [UserTrainingExpiryNotificationService](../../../unified-scheduling/api/Unified.Training/Services/UserTrainingExpiryNotificationService.cs).
- SS notice/excused behavior:
  [TrainingService](../../../legacy/sheriff-scheduling/api/services/usermanagement/TrainingService.cs)
  and [ViewReports](../../../legacy/sheriff-scheduling/web/src/components/MyTeam/ViewReports.vue).
- Target roster capacity validation:
  [AssignmentRequestValidator](../../../unified-scheduling/api/Unified.Scheduling/Validators/AssignmentRequestValidator.cs)
  and [AssignmentDefinitionRequestValidator](../../../unified-scheduling/api/Unified.Scheduling/Validators/AssignmentDefinitionRequestValidator.cs).
