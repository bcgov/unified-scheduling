# Matched ready-v4 Report and actual availability comparison

**Result: Fail. This is measured same-fixture evidence, not migration acceptance.**
Production apply remains disabled. No target application behavior was changed.
Subsequent implementation/future-oracle results:
[guarded graph replay and matched future availability](2026-11-16-graph-replay.md).
The pending-work statements at the end describe this earlier measurement milestone.

## Input and execution provenance

- Independent source: exact `ss_ready_v4`; original adverse captures remain unchanged.
  Oracle owner reports legacy `e6e674c`, 33 passing tests/zero skipped; those source tests
  were not rerun by this process.
- Restricted inputs were copied byte-for-byte into ignored
  `docker/migration/artifacts/`, with owner-only Windows file ACLs before population.
  All four capture hashes were reverified. No restricted rows are committed.
- Full source capture: `ready-v4-source-all-v1-80e9aae.json`, 57 rows,
  SHA256 `DC28370841D1B444D3950E897EDEE2AA268ACC1EDAD7DFDCCDEECA2B1EDC62C7`.
- Availability: `ready-v4-availability-v1-e6e674c.json`, 12 actual SS service calls,
  SHA256 `002C5DB06A157EB1A79413608E66C0E437FFC362C1DB194B4D8E90DF1BB88D24`.
- Target: `unified_ss_rehearsal_ready_v4_rollback`; original restored MyTeam/Training/
  Scheduling lineage remains Completed. No loader was reapplied for this comparison.
- Final Report run **`a92f76fa-ff60-4c24-ab8e-7835fbcd1d2d`**:
  Failed, Scanned74, Rejected0, Failed1 (the comparison validation).
  Earlier matched failures `1aa874a6-52a3-49b8-b99e-776c6840ed0a` and
  `30062109-e356-4350-9d67-82a1b4911119` are retained, not overwritten.
- Source database, synthetic marker and artifact digest are guarded. Actual target
  reads use repeatable-read/read-only transactions. Actual calendar, training,
  WorkingHours and publication-preview services ran, with LoanOut enabled.
  No browser, CSV/PDF, external SSO or actual publication was executed this pass.

## Full Report: strict response comparison

57 source rows / 33 actual target rows; **18 exact, 39 missing, 15 unexpected**.
These are strict response-multiset differences, **not counts of lost business records**.
Crosswalk normalization never expands merged target rows into fabricated SS rows.

| Surface | Source | Target | Exact | Interpretation |
|---|---:|---:|---:|---|
| Duties | 6 | 6 | 6 | All observed response values match |
| Duty slots | 6 | 6 | 0 | All identities, users, intervals and timezones pair; four source flags are unexposed |
| Shifts | 7 | 6 | 0 | One approved merge; three unexposed source fields on all six paired rows |
| OT allocation | 7 | 6 | 6 | One missing source member identity belongs to the merged target shift |
| OT calculated | 6 | 6 | 6 | Actual WorkingHours-derived report values match; all zero in this small fixture |
| Training API | 3 | 3 | 0 | Stored completion facts match; Excused exposure, presentation and derived compliance status differ |
| Training harness projection | 3 | 0 | 0 | No target projection/UI export executed; source projection is not executed SS UI either |
| Distribution conflicts | 15 | 0 | 0 | No executed equivalent target full-distribution adapter; not proof of 15 lost conflicts |
| Distribution sheriffs | 4 | 0 | 0 | Same unsupported full-distribution comparison surface |

### Exact field differences across the three windows

- Slots: `closed`, `notAvailable`, `notRequired`, `overtime`: six differences **each**,
  source false versus unexposed/null target fields. Target capacity is exported separately
  as actual evidence; capacity zero is no longer misrepresented as a source NotRequired flag.
- Shifts: `overtime`, `slotIds`, `workSection`: six differences **each**, unexposed/null
  target fields. One `end` difference is the representative member of the merged shift.
- Training: `excused`, `rowVariant`, `status`: three differences **each**.
  Source false / white / empty status versus target null / null / Active. These are actual
  returned values, not recreated SS compliance/status logic. There is no basis here for
  blaming historical-vs-current clock evaluation: source metadata says current-clock too.
- Three actual training report end instants match `2026-11-01T10:00:00Z`, with null expiry.
  This does not measure all per-record-timezone export cases (BL-14).

### Classification of39 missing /15 unexpected strict rows

| Classification | Missing | Unexpected | Meaning |
|---|---:|---:|---|
| Paired Duty slots | 6 | 6 | Four unexposed source flags per paired row; not six missing assignments/users |
| Paired shifts | 6 | 6 | Three unexposed fields each; one representative end differs due to approved merge |
| Paired training API | 3 | 3 | One completion repeated in three windows; values differ, identity/facts pair |
| Merged member aliases | 2 | 0 | One shift + one OT-allocation identity; complete historical interval union preserved |
| Unexecuted distribution surfaces | 19 | 0 |15 conflicts +4 sheriffs; no equivalent target adapter executed |
| Unexecuted training harness projection | 3 | 0 | Source projection only, not executed UI/CSV evidence |
| **Total** | **39** | **15** | All15 unexpected rows pair to source identities; not unexpected records |

Training **stored business facts** observed equal: user/course/location/region,
completion end `2026-11-01T10:00:00Z`, null expiry. Target `Active` is a **derived
compliance status**, not an enrollment/lifecycle label: actual
`UserTrainingReportQueryHandler.ResolveStatus` uses completion presence and expiry
against the current clock (`NotTaken`/`Active`/`Expired`). Source returned blank.
That label difference alone does not demonstrate a lost certificate or incorrect
compliance result; it also does not establish policy parity. Source metadata uses
current-clock evaluation too, so no historical-clock explanation is claimed.
`rowVariant=white` versus null is presentation. **Excused=false versus unexposed null
is a business eligibility/policy exposure gap**, not merely cosmetic; this fixture
does not validate true-Excused behavior. Future expiry-policy and per-record date
display gaps BL13/BL14 remain. Neither side's harness projection is executed UI/CSV.

Group-aware evidence separately proves that the two historical source shifts and their one
actual target shift have the same location/user/timezone and **identical interval union,
2026-11-02 08:00–16:00 UTC**. Both original member identities occur exactly once in the
source response. No strict rows were expanded and the strict verdict remains Fail.
This proves interval coverage for this group, not every shift calculation or UI behavior.

## BL-15: equivalent windows, distinct actual response planes

The new comparison consumes the exact twelve captured SS windows, including strict bounds
one **second**, not one microsecond, outside the two-hour interval. User/location identities
resolve through actual crosswalks. Actual source marker/selection is checked independently.

Actual target services executed:

1. `UserService.GetAllAsync` — normal home-location resource endpoint.
2. `LoanOutCalendarDataProvider.GetUsersAsync` — borrowed-user resource endpoint;
   accepts inclusive local dates, not SS instant windows.
3. `LoanOutCalendarDataProvider.GetDataAsync` — precise interval calendar read.
4. `CalendarConflictService.GetConflictsForCandidatesAsync` — read-only proposed shift
   for that user and exact query window; no shift creation/override.
5. `ShiftLoanOutAvailabilityService.GetAssignableUserIdsByOccurrenceAsync` — actual
   explicit-candidate guard, **not** user discovery.

Target resolved location timezone is **Etc/UTC**. Older planning/recovery references to
America/Vancouver for this target fixture are superseded by this actual resolver result.

| Observation | Actual SS | Actual target |
|---|---|---|
| Home resource presence, six windows | Present6/6 | Home-user endpoint present6/6 |
| Destination resource presence | Present4/6; absent strict-before/strict-after | Borrowed-user endpoint present6/6 |
| Conflict responses, twelve windows | Four Away instances: inside and whole-day at both locations | Proposed-shift conflict engine returns zero |
| Conflict interval comparison | Each returned interval is exactly 10:00–12:00 UTC | No returned target conflict interval; four differing calls |
| Precise LoanOut calendar | Not the SS availability response contract | At home, inside/whole-day return exact 10:00–12:00 UTC; other queries empty |
| Explicit supplied-candidate guard | No equivalent write-validation call captured | Home rejected6/6; destination allowed6/6 |

**Measured differences:** 2/12 resource-endpoint-union presence differences; 4/12 conflict
count differences and 4/12 interval differences; four source conflict instances versus zero
target conflict instances. Target raw calendar and conflict services are separate planes:
exact stored/calendar timing does not demonstrate equivalent conflict visibility.

The target's actual configured shift conflict rules include holiday/leave, not LoanOut.
No rule/provider or app endpoint was added to force parity. The target borrowed-user
endpoint's date-only request cannot express the strict-before/strict-after SS instant
windows; its observed extra presence is recorded, not silently post-filtered by an invented
SS rule. The resource-endpoint union is a documented backend-contribution comparison,
**not an executed frontend or proof of save authorization**.

The target day-based guard and these source API results warrant BL-15 product review.
They do **not** quantify SS free minutes, prove a user could save a source shift, or approve
future staffing loss. The future adverse probe still needs the actual source oracle.
The migration cannot add exact-instant user discovery or LoanOut conflict participation
without changing target application behavior; recommend an explicit Scheduling/LoanOut
decision/backlog, preserving Fail/NeedsAction until accepted or resolved.

### Future oracle: corrected network handoff

Protected ignored file:
`C:\Dev\CSB\Unified\unified-scheduling\docker\migration\.env.oracle.probe-v4.local`.
Fresh Npgsql reparse printed **`Npgsql.Database=ss_ready_v4_probe`**.
Separate secure metadata check verified Host127.0.0.1/Port5432 and the same Database.
Selected probe database on `unified-ss-migration-source-1` has comment `SS SYNTHETIC`;
reader has SELECT on **22/22 public tables**.

Use **`--network container:unified-ss-migration-source-1`**, loopback port5432,
and wrapper **`-ExpectedDatabase ss_ready_v4_probe`**. The probe database does **not**
exist on original `ss-synth-pg`; using that older network namespace fails. No original
database was copied/reset to conceal that coordination error. No credentials are emitted.

## Retained output digests

All files below are restricted, ignored; names are relative to the artifact directory.

| File | SHA256 |
|---|---|
| `target-report-a92f76fa-ff60-4c24-ab8e-7835fbcd1d2d.json` | `7B066BEC849D8CF43C904082385D853122A39132493454744B00628C17726C6A` |
| `target-report-a92f76fa-ff60-4c24-ab8e-7835fbcd1d2d-comparison.json` | `115636D90B4F08C3C45E3D387FF46D70E617E09BBE3955EB1C100262297C228A` |
| `target-matched-availability-ready-v4-v2.json` | `38334EBB69ABAFAE1079813E8F3BBE80F5FFCFAD2E68D8028179ED98DB4635B2` |

Initial v1 availability output is retained as well; v2 adds explicit interval comparison
and a nonzero diagnostic exit for the measured failure. Nothing was overwritten.
Replay must supply a fresh direct JSON filename through
`DataMigrationRehearsal__MatchedAvailabilityArtifactName`; existing outputs are never replaced.
Actual replay to `target-matched-availability-ready-v4-replay-v2.json` returned the same
measured Fail and was **byte-identical**, SHA256 equal to v2 above.

## Commands and checks

Run from the repository with RTK. Let `BASE` below mean
`rtk docker compose -f docker/migration/compose.yml`; `MATCHED` additionally supplies
`-f docker/migration/ready-v4.yml -f docker/migration/rollback-ready-v4.yml
-f docker/migration/matched-ready-v4.yml`. Runner DLL is
`api/Unified.DataMigration.Runner/bin/Debug/net10.0/Unified.DataMigration.Runner.dll`.

- `BASE run --rm --entrypoint sh runner -c 'dotnet tool restore && dotnet tool run
  csharpier format <four changed runner files> && dotnet build
  api/Unified.DataMigration.Runner/Unified.DataMigration.Runner.csproj --no-restore
  --verbosity quiet'`: **PASS**, SDK10; six existing NU1510 warnings.
- `BASE run --rm --entrypoint dotnet runner test api/Unified.Tests/Unified.Tests.csproj
  --no-restore --filter 'FullyQualifiedName~DataMigration|FullyQualifiedName~Hangfire'
  --verbosity quiet`: **PASS76, failed0, skipped0**. Not new comparator/PG test coverage.
- `MATCHED run --rm --entrypoint dotnet runner <DLL> Report`: **FAIL/exit1**,
  final run above; genuine measured comparison failure.
- `MATCHED run --rm --entrypoint dotnet runner <DLL> availability-compare`:
  **FAIL/exit1**, twelve actual read-service cases, not twelve xUnit tests.
- `BASE run --rm --entrypoint dotnet runner <DLL> verify-oracle-probe`:
  **PASS**, persisted Npgsql Database verified; not a connection test.
- `rtk proxy powershell -NoProfile -File docker/migration/Import-ReadyOracleArtifacts.ps1`:
  **PASS**, four original SHA256 values reverified.
- Source metadata SELECT through psql: **PASS** on migration source; original
  `ss-synth-pg` probe check failed because that named database is absent (see network fix).
- Parent backlog/training/gaps/validation/contract mirrors: **PASS**, all five text-equal
  after line-ending normalization. They retain the parent's historical pending statements;
  this report supersedes those runtime statements, without editing parent-owned files.
- Initial formatter run lacked per-container tool restoration; corrected rerun passed.
  Initial mirror-validation Python quoting failed before evaluation; corrected check passed.
- Raw `rtk git diff --check` flagged CRLF endings as whitespace; repository docs already
  use CRLF. `rtk proxy git -c core.whitespace=cr-at-eol diff --check`: **PASS** after
  build/tests. Restricted inputs, env and outputs confirmed ignored. Artifact directory,
  probe env and final availability output ACLs have
  zero Allow rules for identities other than the current owner.

UnitTests follow-up: automated comparator/adapter, interval identity ambiguity and broader
PostgreSQL regressions remain; the twelve actual calls above are deliberately not counted
as automated test coverage. No design-system/UI review is implied by backend service calls.

## Explicitly outside this completed bounded measurement

Multi-root repartition/merge/split, multi-entry Duty replay, changed-series replay and isolated
dry-run parent candidates remain incomplete. Broader inverse/concurrency/private-photo
coverage remains. SH-08 still fails24/26 comparisons and SH-01..07 remain unmeasured:
the synthetic source is **not production-shape-fit**. Full adverse target rehearsal and
future actual-source availability remain pending. No new app behavior, shared DB reset,
GitOps change, attribution or push. Original contract-file deletion remains unstaged.
