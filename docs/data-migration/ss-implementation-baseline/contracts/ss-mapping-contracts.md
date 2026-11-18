# SS Mapping Contracts

**Baseline: 2026-11-16. Implementation authorized by the current request; production
writes are not.** This is the canonical contract for FR-015/T016. It incorporates the
final [worksheet](../../../documentation/data-migration/ss-job-notes/mapping-decision-worksheet.md)
and [multi-job design](../../../documentation/data-migration/migration-job-design.md#ss-job-split-accepted-2026-11-16).
Earlier notes marked pending are history where the rules below settle them. CASS is deferred.

## Safety And Identity

- Read SS only. Use explicit projections, read-only transactions and a read-only login.
- Source identity is `(SS, EntityType, LegacyId)`. Generate target IDs and resolve every
  supplied relationship through durable crosswalks. Equal internal IDs prove nothing.
- Preserve all existing references, including expired rows, audits and ordering. Link or
  insert only; never reactivate, reparent or overwrite an existing reference.
- Missing/ambiguous identities, invalid required values, dangling links and parent cycles
  reject with safe source-keyed evidence. Continue independent rows, retry rejected and
  dependent rows independently of watermarks, and never count rejection as success.
- A batch's targets, crosswalks, mutations, counts and progress commit together. Roll back
  the entire batch on failure; persist redacted failure evidence separately.
- Unified owns audits/concurrency. Never copy source audit metadata. No names, comments,
  photos, credentials or raw payloads in logs/findings. Photo bytes may be copied to Users
  only; do not emit them or include them in evidence hashes.
- Q-X4: for job-created non-reference targets, compare mapped source and current target
  fingerprints against the last applied fingerprint inside the transaction. Unchanged
  source means no write. Changed source may update only an unchanged target. Otherwise
  preserve the target and record a conflict. Linked-only targets never update.
- Merged groups have one fingerprint covering all members and one crosswalk per source row.
  Categories/series use stable synthetic source keys. Do not select only changed members.
- Physical absence is not a delete instruction. Apply only the explicit lifecycle rules.
- Exclude SS Audit, JcSynchronization and DataProtectionKeys as system-owned tables;
  reconcile exclusion counts only, never copy payloads, key rings or JC state. Unified
  retains its own audit, synchronization and cryptographic state (D-13).

## Job And Date Contract

All jobs use `Source=SS`, shared live crosswalks and table progress. Each entity has exactly
one job owner. See [plan](../plan.md) for gates, dry runs and rollback.

| Job | Entity ownership |
|---|---|
| `data-migration:ss:myteam` | Region, Location, Type 5, Type 7, Role, User, UserRole, acting rank, leave, away location; permissions excluded |
| `data-migration:ss:training` | Type 6, categories/profile links, SheriffTraining; standard-training policy |
| `data-migration:ss:scheduling` | Type 0, Types 1-4 classification, Assignment, Shift, Duty, DutySlot, series |
| `data-migration:ss:report` | Read-only business reconciliation; writes migration findings/exports only |

`CutoverDate=2026-12-01` is required and fixed across jobs and rehearsals. Local event dates
on/before it are Active; later dates are Draft; expired rows are Cancelled. Series start no
earlier than that date. Retained leave-type expiry is that date at UTC midnight. Do not
recompute these values from the run clock. Refuse a changed cutover after applied work until
affected lineages are rolled back. Insert-only null date/photo fallbacks use one UTC run start.

`HistoryStartDate=2020-01-01` means midnight America/Vancouver, initially
`2020-01-01T08:00:00Z`. Include EndDate >= boundary regardless of row timezone for acting rank,
away, leave, training, Duty, DutySlot and Shift. Include unexpired Assignments and every
Assignment needed by an included Duty. People, roles, UserRole and needed references are
not history-filtered. Earlier cutoff changes trigger resumable backfill independent of normal
incremental progress; later changes do not delete targets. Boundary moved from 2015-01-01 to 2020-01-01 on 2026-11-17.

Copy DateTimeOffset instants as UTC. Preserve supplied valid IANA timezone; reject invalid
values. Q-X1: person-event null timezone stays null (read-path fallback must be checked);
blank is invalid. Roster timezone null, blank or invalid rejects. This is not permission
to reinterpret source instants or substitute a host timezone.
Reject inverted/equal intervals except training permits equal start/end. No invented intervals.

## References And JC (T051)

### Config-Based JC Ownership (T051)

Before any SS business write, verify `JCInterface:ExpireRegions`, `ExpireLocations` and
`ExpireCourtRooms` are explicitly false. Missing/true values fail closed. No per-row ownership
field, classification or backfill. Matching-key JC updates/reactivation remain accepted.
Schedule JC apart operationally; it is not part of the migration DB dependency gate.

The older requirement to change the JC code default is now a **backlog-only app fix**:
the current request forbids non-migration implementation changes. Explicit false config plus
migration preflight protects this migration; no JC implementation edit is authorized.

| Source | Match and permitted insert |
|---|---|
| Region, non-null JustinId | Unique verified JustinId |
| Region, null JustinId | Valid existing link, else unique exact Name among null-JustinId targets (expired included), else insert. Duplicate exact source names reject |
| Location | Unique verified AgencyId, never name/JustinCode. Resolve Region and parents before insert |
| Type 0 | Exact Code -> CourtRooms.Room plus resolved LocationId; null matches null only |

Region inserts copy Name, JustinId, ExpiryDate; omit Region.Code from fields, identity,
hashes and evidence. Location inserts copy AgencyId, Name, JustinCode -> JustinLocationCode,
Timezone, resolved RegionId/ParentLocationId, ExpiryDate. Preserve source nulls; unresolved
supplied relationships reject, never become null.

Type 0 links a unique existing room unchanged. Insert a valid missing active room or one
referenced by any Assignment; copy exact Code, resolved location and lifecycle. A missing
expired unreferenced room is Excluded and re-evaluated next run. Ambiguity/unresolved location
rejects. No JC feed or per-room approval needed.

## Lookup Types

### Types 1-4 And Assignments: Approved Two-Step Plan

Step 1 classifies lookups; Step 2 loads definitions, duties and staff links. Lookup
classification never replaces the source Assignment identity.

Use the reviewed exact `(Type, Code)` classification table from the
[assignment decisions](../../../documentation/data-migration/ss-job-notes/mapping-decision-worksheet.md#types-1-4-and-assignments-open-gaps).
Persist frozen classifications; no runtime keyword guessing, Stats seed writes or group 3.
Resolve actual seeded category/subcategory identities and verify their relationship.
Documents use C5/22; loan-out assignment codes C12/44; Type 0 assignments C1/3;
sergeant codes use Supervision, Inspector/Staff Sergeant/Supervisor use C37/135.
Leave Waitlist and Test assignments and dependants are Excluded with counts/IDs.
Unused codes need no mapping. An unknown used code raises a visible decision finding.
The user permits documented logical Stats assumptions: unresolved classification alone must
not conceal otherwise transferable scheduling data or justify invented business facts.

### Type 5: LeaveTypes

The [44-row accepted snapshot](../../../documentation/data-migration/ss-job-notes/leave-mapping-review.md#accepted-production-snapshot-mappings)
is the allowlist. Seven links: STIIP -> STIIP; A/L and CTO -> Vacation/CTO; LWOP -> LWOP;
Medical Under 2 Hrs and Medical Over 2 Hrs -> Med/Dent; Special Leave -> Special.
Also retain the earlier approved aliases Special -> Special, STIP -> STIIP,
Annual -> Vacation/CTO. Other earlier-list labels are not approved.

The remaining 37 snapshot types insert as distinct expired paid types, including Illness,
All Day Medical and SPL Parental Leave. Only LWOP is unpaid. Trim Code ends only; require
nonblank/max 50, no truncation. Require LocationId and SubCode null (even empty is non-null).
Missing/ambiguous/inconsistent canonical standards, unexpected retained-name collisions,
duplicate trimmed names and unlisted codes reject; no guessed alias or paid status.
Resolve valid links first. Startup seeds define standard targets but still check them.

New retained Description is trimmed, null/blank falls back to trimmed Code, and may be
truncated to 100 without splitting a surrogate pair; trim the exposed trailing whitespace.
EffectiveDate=`1900-01-01T00:00:00Z`; ExpiryDate=`2026-12-01T00:00:00Z`.
Replay preserves inserted dates and fields. Existing references never mutate.
Source sort rows are omitted and counted. Future leave retains its event, even when its
type is expired; record any edit/picker loss as a Unified backlog item.

### Type 6: Trainings

- Nonblank Code of <=50 UTF-16 units copies exactly, without trim/case folding.
  Approved 2026-11-17: longer codes use the longest leading substring fitting <=45
  UTF-16 units without splitting a surrogate pair, plus `_1`, `_2`, etc. In numeric
  source-ID order, take the first suffix not used by any source Type 6 code or
  target course (including retired rows and earlier inserts). Allocate under the
  existing target table lock; never merge a generated match with another course.
  Reject `CourseCodeSuffixExhausted` if no suffix fits 50 units. New shortened
  courses report `MigratedWithDifference:CourseCodeShortened`. Hash raw source
  values; replay existing crosswalks before allocation so later manual target
  renames stay intact. Duplicate exact source Codes still reject rather than merge.
  LocationId and SubCode must both be null. Source-only SQL previews assume an
  empty target/no crosswalks; they cannot predict final target-aware suffixes.
- `Carbine` in an overlength source code still qualifies its migrated history for
  user-profile assignment even when shortening/manual cleanup removes that keyword.
- Preserve existing course fields/links. Insert missing valid courses, expired included.
- Description copies exactly, null -> empty, max 200 without truncation. Supplied
  timestamptz copies its UTC instant; null EffectiveDate -> fixed UTC run start.
  Copy source course expiry exactly, including null.
- Copy Mandatory. New Rotating=true if source true, ValidityPeriod>0, or repeated eligible
  same-user/course history exists; otherwise false.
- ValidityPeriod 0 -> null, 1461 -> 1460, >1825 -> 1825; other positive values unchanged;
  negatives reject. AdvanceNotice 0 -> null; positive must be below mapped non-null
  validity, or any positive when validity null; negatives reject.
- Append Order after maximum including expired rows, empty starts 0, ascending numeric
  source ID within each batch. Use a PostgreSQL table lock that conflicts with application
  writes before MAX/read through commit; a migration-only advisory lock is insufficient.
  Integer exhaustion rejects without renumbering.
- Q-T6C client gate **cleared 2026-11-15**: exact names Provincial Standard, Branch and Local
  are approved (snapshot 22/4/4 courses; 42 blank). Null/blank -> no category; exact-name
  reuse or insert generated ID/null Description/max 100. No catalogue replacement.
  Set category only on newly inserted courses; category/course/link commit together.
- For newly inserted mandatory exact Codes Carbine Initial Course, Carbine Practice 1,
  Carbine Practice 2, Carbine Annual Recertification, link active CARBINE_OPERATOR.
  No links for Carbine Selection or Carbine Operator Course. Missing/inactive profile or
  listed non-mandatory course rejects. Existing targets and links remain unchanged.

### Type 7: PositionTypes

Ignore LocationId and SubCode in identity, validation, copies and hashes. Match exact Code,
reuse existing unchanged or insert valid missing position. Code max 50, Description max 100
(null -> empty), supplied dates copied, null EffectiveDate -> run start, generated ID.
Append DisplayOrder after maximum including expired, empty starts 1; serialize with ordinary
writers as for courses; reject overflow.

Resolve base/acting rank text by unique exact SS Type 7 Code first, else unique exact
Description. Include expired rows; no trimming, fuzzy match or location tie-break. An invalid
Code match cannot fall back. Unknown/ambiguous text rejects, never invents a rank.
Write resolved target Code to Users.Rank and actual position ID to acting positions.

No LookupSortOrder is copied: Types 0/1-4/5 omit it; Types 6/7 append. Count omissions.

## My Team

Migrate every user type, enabled or disabled, except system user
`00000000-0000-0000-0000-000000000001`; resolve the system actor to Unified's system user.
Normalize IdirName using Unified's trim/lowercase convention; it is the sole match key.
Generate new user GUIDs. Null/blank names and IdirName log findings, null -> empty; any
normalized collision rejects all colliding users. Preserve IdirId/KeyCloakId with unique
collision checks; no authentication token/password invention. Copy enabled state, email,
badge, last login and resolved home location; null Gender -> Other, never CLR Male default.
Copy photo bytes privately; absent photo -> null LastPhotoUpdate, valid timestamp copies,
default/min timestamp -> run start. EmployeeNumber null; PendingRegistration=false.
No UserCreatedSignal and no invented mandatory qualifications.

Excused users still migrate; log source IDs only. Training assigns CARBINE_OPERATOR only
to migrated users with non-deleted, non-excused Carbine history ending on/after cutoff;
preserve existing non-null profile. This target field is owned solely by Training, avoiding
concurrent ownership with My Team.

Roles: Super Admin -> existing Administrator; Administrator -> Sheriff Administrator;
Manager -> Manager; Sheriff -> Staff. Other names reuse a unique active case-insensitive
match or insert same-name role. Newly created roles receive a one-time current Unified Staff
permission snapshot. Do not import Permission/RolePermission or resync permissions.
UserRole copies all effective/expiry/reason values, including expired/future rows; no cutoff.

Acting positions copy start/end, timezone, comment, expiry/reason and resolved user/position.
Away and leave create Event plus detail plus crosswalk atomically. **Current-model correction
(2026-11-16):** away uses LoanOutEntry + Event + one user link, destination in
LoanOutLocationId and source/home in Event.LocationId. The older UserAwayLocations proposal
does not cover the current LoanOut-enabled workflow. Use actual current event/module codes
and read paths rather than creating a dormant legacy model row.
Do not invoke ordinary LoanOut creation: it forces all-day and transfers shifts.
Copy source instants/cancellation without those side effects. Day-based availability may
differ for partial-day records; measure/report affected intervals and future impacts (BL-15),
never claim exact availability merely because the stored Event interval is exact.
Resolve user/location/type;
null required away location/leave type rejects. AllDay=false; preserve times/timezone/notes.
Titles follow the current LoanOut convention and `{FirstName LastName} leave` (fallback Leave).
Leave is standalone with one LeaveEntryUser. Soft-deleted rows become cancelled events with
copied cancellation instant/reason; no invented cancelled actor.

## Training History And Reporting

Skip/count records with own ExpiryDate non-null and every record of an Excused=true sheriff;
no new target/link. If later excluded after migration, preserve old targets/links and warn.
Course retirement and past certificate expiry do not exclude eligible histories.

Require resolved user and Type 6 course. StartDate -> AwardedOn; EndDate -> EndingOn;
equality allowed, reversed dates reject. Copy TrainingCertificationExpiry exactly, including
null, with no expiry calculation. FirstNotice false -> None, true -> Sent, never Pending.
For Other course, Comment -> `Other training: {Comment}`, append newline + distinct Note;
if equal include labeled Comment once; absent Comment copies Note. Preserve source text.
A Comment on another course rejects. Validate target lengths rather than truncate.

Keep every eligible history distinct by source ID. Within user/course, allocate versions
in EndDate, StartDate, source-ID order after existing maximum (empty starts 1); never renumber.
A later older row gets max+1 plus out-of-order finding. Use tracked migration inserts with
audit/FK/unique validation; do not invoke renewal or user-creation side effects.

SheriffStandardTraining is intentionally not migrated/reported separately. Unified mandatory
flags/profile applicability replace future-user rules; stored SS histories still migrate.
Compare actual SS service/export and actual Unified report results, not just row counts.
Missing mandatory rows, repeats, disabled/exempt/excused users, categories, notes, timezone
dates and exact certificate expiry are acceptance cases. SS grace-year/status differences
and auto-created standard histories must be named, not presented as new attendance facts.

## Scheduling

1. **Definitions G-8/9/11.** Assignment identity flows through its own crosswalk, not
   the classification lookup. Merge by resolved location, lookup type and
   upper(trim(proposed display name)); one crosswalk per member. Select casing from live
   member, else latest, with deterministic source-ID tie-break. Split cross-type name
   collisions with type suffix; enforce max 100/normalized uniqueness without silent loss.
   DefaultCapacity=1. EffectiveDateUtc=1900-01-01. Exclusive valid-to is earliest source
   expiry / inclusive ad hoc end +1 day, UTC midnight; merged group is open if any member
   live, otherwise latest member valid-to. Widen to cover non-cancelled migrated duties
   and report. Merged defaults use most-common duty times, deterministic time tie-break;
   singleton defaults copy. Assignment.Comment -> Description exactly.
2. **Shifts first G-17/18/19.** Group user/location/local date; merge identical,
   overlapping and touching spans only. Earliest start/latest end, join distinct comments,
   keep one link per source shift. Do not count unworked gaps as work. Past gap-free blocks
   remain separate Active shifts with findings; future gap groups are NeedsAction and held
   for scheduler repair, never silently dropped or auto-filled. Different locations stay
   separate. AllDay=false; title `{employeeName} shift`, fallback Shift.
   LunchAvailableMinutes=60; WorkedLunchMinutes=0. Verify working-day threshold 420.
   Never write stored overtime. Report every changed sheriff-day with stored and computed
   hours: accepted production baseline 84 past days/-388.3h (34 gap -45.8; 1 overlap -8;
   49 stored-exceeds-rule -334.5). No unapproved future overtime difference.
3. **Duties G-12.** Require AssignmentId. Zero/one live slot -> one entry over duty times,
   capacity 1; Closed/NotRequired -> 0; NotAvailable -> 1 with no user. Multiple live slots
   -> one entry per distinct sheriff spanning that sheriff's slots, capacity 1. Repeated
   sheriff segments merge; flag-only segments beside staffed segments drop with findings.
   No staffed slot -> single entry. Title=definition name; Duty.Comment -> Event.Description.
   Stamp classification on definition/series/entry.
4. **Links G-13/16.** After shifts and entries, link staffed slots through
   ShiftAssignmentEntry and ShiftAssignmentEntryUser to containing/overlapping merged shifts.
   Deduplicate identical links. Partial-outside coverage reports a difference. No-shift
   slots retain an unfilled entry and approved DroppedBadData finding: material break is
   **>0.1% of filled slots OR any future slot**, which blocks acceptance. Insert/report
   historical FTE/merge-induced uniqueness conflicts directly; do not bypass future conflicts.
   Omit slot overtime flags/stored shift overtime with counts. Non-null
   AnticipatedAssignmentId is a finding, not an inferred link.
5. **Series G-10.** Only current-to-future local dates >= cutover; qualify an assignment
   only when every RRULE occurrence through the last occurrence has a matching live SS duty
   inside weekday/time/ad hoc pattern. No invented/regenerated occurrence. Extra FTE,
   off-pattern/non-qualifying duties and all history remain standalone.
   Insert EventSeries/AssignmentSeries and link existing migrated entries without regeneration.
   If Unified cannot hold an Active cutover-day occurrence, start next local date and keep
   the cutover entry standalone Active (SS-V052). Record actual compatibility evidence.

## Validity Gate

Every selected source row has exactly one final outcome: Migrated, Merged,
MigratedWithDifference, Excluded, DroppedBadData, Rejected, Failed or NeedsAction.
Use separate group-metric findings so overtime does not double-count row coverage.
Synthetic manifests have fixture-specific expected counts, **not production thresholds**.
Production extract totals are evidence baselines, not hardcoded pass values for fake data.

Fail acceptance on unexplained differences, missing coverage, unknown/rejected/failed rows,
threshold breaches or unmet identity/schedule/training goals. Known approved differences may
yield PassWithKnownDifferences only within declared limits and with **zero open NeedsAction**.
An unresolved action yields NeedsAction, never Pass. Noncritical Stats gaps remain visible
backlog work. No test skip, hardcoded result, data repair or target-app change may fake a pass.
Use [validation register](../../../documentation/data-migration/ss-job-notes/validation-tests.md)
SS-V001..SS-V057 as traceability; pending checks stay pending until actual evidence exists.
