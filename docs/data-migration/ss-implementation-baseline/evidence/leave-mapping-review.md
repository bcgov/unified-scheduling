# SS Leave Mapping: Product-Owner Review

**Status: Mapping decisions closed 2026-11-15.** The 44 accepted snapshot pairings cover every
Type 5 row in production. Q-L1 to Q-L6 (worksheet) settle the remaining field, scope,
collision, sort-order and unknown-code rules. Earlier-list codes not in the snapshot do not
exist in production and need no mapping; if one appears, Q-L6 raises an exception. No
product-owner review remains. Pre-write checks stay deferred under SS-V015.

Paused by the developer on 2026-10-29 for product-owner clarification. On 2026-11-10 the
user accepted the snapshot mappings below, including the later corrections, and approved
trimming names at both ends. The full contract remains incomplete; this does not authorize
implementation, test runs, or database writes.

## Evidence And Existing Approval

The earlier 18 codes below were supplied by the user. Source environment, IDs, location/SubCode
scope, expiry, and usage counts were not supplied or independently checked. This list is
not a fresh database extract and is distinct from the four checked-in SS leave seeds.

Earlier approved aliases remain: `Special -> Special`, `STIP -> STIIP`, and
`Annual -> Vacation/CTO`. The former `Illness -> Med/Dent` approval was superseded on
2026-11-10: `Illness` is to be retained as a distinct expired, paid type. The supplied
list contains `STIIP`, not the older seeded spelling `STIP`; preserve that distinction.

## Accepted Production Snapshot Mappings

The following user-supplied mapping covers the 44 Type 5 rows in the 2026-11-09
[production extract](./mapping-extracts.md#ex-07-type-5-leave-catalogue-ss-v015).
It is a snapshot, not an automatic rule for future source codes. The user's corrections
retain `SPL Parental Leave`, `All Day Medical`, and `Illness` as distinct expired, paid
types, rather than mapping them to `Special` or `Med/Dent`.

`Retain expired` specifies the desired distinct target, not permission to change an existing
Unified reference. Effective date, expiry date, and Description are approved below; remaining
insert defaults, name collisions, and matching details remain open. The `IsPaid` values are
user-supplied values for the mapping, not evidence of source payroll treatment or deployed
target values. Existing Unified references remain unchanged, including their `IsPaid` and expiry.

`[spaces]` marks source codes for which the extract flags leading or trailing spaces; the
display label does not show the exact whitespace. For Type 5, trim leading and trailing
whitespace from the source name for mapping and new target names; do not change case or
interior whitespace. Do not modify the source or rename an existing Unified target.
Resolve existing crosswalks by source ID first; report ambiguous or conflicting identities
instead of silently merging or relinking them.

### Active SS Types In The Snapshot

| SS type | Unified target or action | IsPaid |
|---|---|---|
| `STIIP` | `STIIP` | Yes |
| `A/L` | `Vacation/CTO` | Yes |
| `CTO` | `Vacation/CTO` | Yes |
| `SPL Parental Leave` | Retain expired | Yes |
| `LWOP` | `LWOP` | No |
| `Medical Under 2 Hrs` | `Med/Dent` | Yes |
| `Medical Over 2 Hrs` | `Med/Dent` | Yes |
| `LTD` | Retain expired | Yes |
| `WCB` | Retain expired | Yes |
| `Bereavement` | Retain expired | Yes |
| `Supplemental Leave` [spaces] | Retain expired | Yes |
| `Part Time Away` | Retain expired | Yes |
| `Union Leave` | Retain expired | Yes |
| `Other Leave (Specify with Note)` | Retain expired | Yes |
| `Special Leave` | `Special` | Yes |
| `Carbine Instructor Course` | Retain expired | Yes |
| `Pistol Instructor Course` | Retain expired | Yes |

### Expired SS Types In The Snapshot

| SS type | Unified target or action | IsPaid |
|---|---|---|
| `All Day Medical` | Retain expired | Yes |
| `ATT - Advance Driver Training` | Retain expired | Yes |
| `ATT Instructor` | Retain expired | Yes |
| `Auxiliary - Day Off` | Retain expired | Yes |
| `Birth of Child` | Retain expired | Yes |
| `Cops for Cancer Ride` | Retain expired | Yes |
| `Covid` | Retain expired | Yes |
| `CTO carryover` | Retain expired | Yes |
| `CTO Prior Year` | Retain expired | Yes |
| `Day Off` | Retain expired | Yes |
| `ESA Sick Leave` | Retain expired | Yes |
| `Family Illness` | Retain expired | Yes |
| `Flex Day` | Retain expired | Yes |
| `FRO Inst.` | Retain expired | Yes |
| `Funeral Leave` [spaces] | Retain expired | Yes |
| `ICS 100` | Retain expired | Yes |
| `Illness` | Retain expired | Yes |
| `JI training` | Retain expired | Yes |
| `Loan Out - TA` [spaces] | Retain expired | Yes |
| `MOVE DAY` [spaces] | Retain expired | Yes |
| `Parental` [spaces] | Retain expired | Yes |
| `Prior year A/L` | Retain expired | Yes |
| `PSU Initial Course` | Retain expired | Yes |
| `PT` | Retain expired | Yes |
| `PT AWAY` | Retain expired | Yes |
| `SCMS` | Retain expired | Yes |
| `SCMS Training` | Retain expired | Yes |

The mapping retains 37 types as expired and paid, including 10 active SS types, and links
the other seven to standard targets. Of the five `[spaces]` rows, `Supplemental Leave` was
active in the SS snapshot; its intended Unified target is expired.

The user expects SS to be end-of-life after cutover, with no likely future codes, and states
that target names do not collide. These are planning inputs, not verified target data. Report
unexpected new codes and collisions for review; do not silently invent a mapping or change an
existing target. Existing target collisions can prevent the desired expired or paid state.

The source snapshot has 243 leave rows starting in 2027, but does not show how many use
the retained types. Expiry does not cancel linked leave; it can make the type unavailable
when editing a leave dated on or after the expiry date. The current
[leave form](../../../unified-scheduling/web/src/modules/leave/components/LeaveForm.vue)
can clear an unavailable selected type during editing.

### Approved Dates For New Retained Types

Approved by the user on 2026-11-10:

- Set `EffectiveDate` to the synthetic business date `1900-01-01T00:00:00Z`. This is not
  an SS source date; it permits leave dates and backfill from 1900 onward.
- Capture the migration run-start instant once. Convert it to the calendar date in
  `America/Vancouver` and store that date at UTC midnight as `ExpiryDate`. Expiry is
  exclusive, so the new type is unavailable for leave dated on or after that date.
- Keep both dates unchanged on replay. Do not copy SS `EffectiveDate` or `ExpiryDate`.

The 2015 history cutoff can move earlier, so it is not used as the new type's effective date.

### Approved Description For New Retained Types

Approved by the user on 2026-11-10:

- Trim the SS Description at both ends. If it is null or blank, use the trimmed SS Code.
- If the result exceeds the Unified 100-character validation limit, keep the leading text
  that fits rather than reject it. Do not append a marker or split a Unicode surrogate
  pair; remove any trailing whitespace exposed by the cut. The result must remain nonblank
  and pass validation.
- This applies only to new retained types. Keep existing target Descriptions unchanged,
  including on replay. The approval does not permit truncating Code or other types' fields.

## Earlier Supplied Catalogue And Pairing Status

**Superseded 2026-11-15 (history only).** The `Pending` and `Candidate` statuses below are not open
work. Codes in this list that appear in the production snapshot are covered by the 44 accepted
pairings. The others (for example `Maternity Leave`, `Parental Leave`, `Flex`, `Overtime`,
`Training`, `Off`, `SPL`) do not exist in production and need no mapping. If one appears at run
time, Q-L6 raises an exception.

Rows marked `Candidate` are proposals only. Exact target-code availability does not settle
source scope, paid status for new rows, or the full leave contract.

| SS code as supplied | Unified target or candidate | Status |
|---|---|---|
| `CTO Previous Year` | No approved mapping | Not the snapshot's `CTO Prior Year` |
| `Maternity Leave` | No clear existing type | Pending product-owner decision |
| `A/L` | `Vacation/CTO` | Approved for the snapshot |
| `Med/Dental` | Candidate `Med/Dent` | Pending alias |
| `Overtime` | No clear leave type | Pending meaning/target decision |
| `STIIP` | `STIIP` | Approved for the snapshot; row-level rules open |
| `Annual` | `Vacation/CTO` | Existing approved alias |
| `Illness` | Retain expired | Approved; previous `Med/Dent` alias superseded |
| `Special` | `Special` | Existing approved alias |
| `Parental Leave` | No clear existing type | Pending product-owner decision |
| `Doctor Appointment less than 2 hours ` | Candidate `Med/Dent` | Not in the snapshot; mapping pending |
| `Bereavement` | Retain expired | Approved for the snapshot; insertion rules pending |
| `SPL` | Candidate `Special` | Pending abbreviation/alias confirmation |
| `LWOP` | `LWOP` | Approved for the snapshot; row-level rules open |
| `Training` | No clear leave type | Pending meaning/target decision |
| `CTO` | `Vacation/CTO` | Approved for the snapshot |
| `Off` | No clear leave type | Pending meaning/target decision |
| `Flex` | No clear existing type | Pending product-owner decision |

Names from this earlier list that do not occur in the snapshot are not approved merely
because a similar snapshot name was mapped. The doctor-appointment code has one trailing
space inside the backticks; the approved Type 5 trim does not itself approve its mapping.

## Product-Owner Decisions Required (Closed 2026-11-15)

Resolved by Q-L1 to Q-L6 in the [mapping worksheet](./mapping-decision-worksheet.md): guard
exceptions for invalid Code and non-null LocationId/SubCode (Q-L1, Q-L4), seeded standard targets
(Q-L2), collision exception (Q-L3), sort order omitted (Q-L5), unknown code exception (Q-L6).
Original list kept for history:

- Approve remaining new-row defaults and missing/invalid-value rules.
- Resolve location/SubCode variants, missing or conflicting targets, and sort-order handling.
  Existing Unified targets remain unchanged.
- Review any earlier-list or new source codes not covered by the accepted snapshot or
  earlier approved aliases; do not infer meanings, paid status, or a target from similar names.
- Before writes, check actual target-name collisions and future-dated leave using retained
  types. Keep these checks deferred under SS-V015 until validation is authorized.

## Completion Gate

Met 2026-11-15 for mapping decisions; contract write-up is pending the final consolidation.
Original gate: keep Type 5 and T016 incomplete until product-owner answers are recorded in the
[canonical SS contract](../../../specs/001-data-migration-jobs/contracts/ss-mapping-contracts.md)
and [lookup worksheet](../../../specs/001-data-migration-jobs/contracts/lookup-mapping-clarifications.md).
Then refine deferred check SS-V015 in the [validation list](./validation-tests.md).
The accepted pairings and trim rule do not approve the full Type 5 contract, authorize
mapping implementation, or record a test result.

## Next Mapping Review

Continue Type 5 with the source LocationId/SubCode scope decision, then resolve remaining
insert and collision rules. The production snapshot has null values for both fields on all
44 Type 5 rows; the target has neither field. A null-only rule with exceptions for non-null
values is a proposal, not yet approved. See the [training review](./training-field-review.md)
and canonical contract for current Type 6 approvals and remaining holds.

For batch review, use the [remaining-decision worksheet](./mapping-decision-worksheet.md).
Its Decision cells are blank; proposed rules there do not change existing approvals.