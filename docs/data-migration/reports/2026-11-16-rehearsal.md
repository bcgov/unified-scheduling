# Measured SS rehearsal — 2026-11-16

**Verdict: Fail / NeedsAction.** Ready-fixture inserts are working; full acceptance is not.

## Latest implementation-phase evidence (supersedes earlier limitations below)

The older run sections are historical evidence, not current mapper behavior. In particular,
missing Sheriff Administrator was a BUG (now fixed), away now uses the real LoanOut model,
qualified series and actual publication now work, and the obsolete-constructor test failures
were fixed. Earlier adverse databases/runs/captures have not been erased.

### Ready-v3 and actual application paths

Source `ss_ready_v3`; target `unified_ss_rehearsal_ready_v3`, both persistent.

| Job/path | Run or resource | Measured result |
|---|---|---|
| MyTeam | `b18ee1cb-a4b7-4ee4-b887-d3dc97fb8810` | Completed; 41 business rows; zero rejected/failed |
| Training | `fc781db2-7846-46ff-af66-2bdcba0de1d9` | Completed; 10 business rows; zero rejected/failed |
| Scheduling replay | `4c4b9e40-0b59-4b6e-95ca-5da1ba733ab8` | Completed; 15 business rows; group/read metrics separate (older stored Scanned=49 included metrics; counting now fixed) |
| LoanOut-enabled services | Actual availability + LoanOut calendar provider | Destination assignable 1, home assignable 0; exact timed source interval returned |
| G10 assignment series | Existing Dec 2/3 entries | One verified draft series, no generated duties |
| Actual publication | `unified_ss_rehearsal_ready_v3_publish` clone | Preview CanPublish=true, zero conflicts; published two shifts/two assignments |
| Report | `4ff44590-06de-459a-964c-35ee5fc8c094` | Failed; 66 source rows covered, no rejected primary rows, one failed comparison metric |

Report invoked actual services and the independent source-capture comparison. The retained
capture is from ADVERSE `ss`, not ready-v3: 22,868 source rows versus 23 actual ready target
rows, 22,868 missing/23 unexpected, seven explicit gaps including fixture mismatch.
This is diagnostic FAIL evidence, not parity. Export against the empty adverse target
likewise failed: 22,868 missing, zero target rows, six gaps.

Parent-supplied source oracle result: 29 tests passed, zero skipped, including two actual
PostgreSQL tests; legacy commits `e2da417`, `9a57c54`. This is separately owned evidence,
not part of the Unified test count. Restricted captures are ignored, never committed.
Training capture has 6,516 rows; all-surfaces capture has 22,868. Contract mirror:
[oracle-target-export](../ss-implementation-baseline/evidence/oracle-target-export.md).
NonExcused/UI projection is a harness projection, not executed UI/CSV evidence.

Canonical windows are Nov 1..30, Nov 17..Dec 14, and **Jan 1..31, 2027**. The December
future-month convenience window used earlier below was not the supplied oracle window.
Ready-v3 deliberately preserves exact UTC instants; there is no January roster in it yet.

### Owned update and external drift

Persistent separate clones: source `ss_ready_v3_drift`, target
`unified_ss_rehearsal_ready_v3_drift`; positive ready-v3 was not changed.

| Run | Result |
|---|---|
| `96a4e9cd-049a-4c70-a680-9d5f63fbb1e5` | Completed, 42 rows; new source UserRole 900010 inserted with original row-version proof |
| `8cef5b00-ba52-4d5c-9da4-84b636a80e8e` | Completed, 42 rows; same target ID 5001 updated to source expiry 2032-01-01; Insert/Update mutation proofs present |
| `0e2ddc6f-6be5-4308-92ec-5a1afb0c05af` | Failed, one rejected; target text independently changed, next source date update rejected; 2032 date/external text preserved, mutation count remains two |

No private text/photo is hashed or logged. Original PostgreSQL root/event versions plus
mapped structural hashes are checked under row locks. Existing records without proof reject
updates. This is selected-entity support, not complete Q-X4 or rollback coverage.

Later counted-update run `57a0c91a-646a-4c8d-b510-6d1f1759b448` retained that drift rejection
and updated another independent owned UserRole: persisted UpdatedCount=1, RejectedCount=1,
one OwnedMappedUpdate and one OwnedTargetDrift outcome. Provider FailedCount=0.

### Corrected adverse dry run

Latest `9b888c46-481a-409d-a3c8-af12745ba633`: **Failed**, 2,233 source rows,
48 Rejected, zero provider Failed, zero blanket away NeedsAction. Real LoanOut enabled.
Three roles migrate; 108 UserRoles migrate/two reject unresolved parents. Of 183 away rows,
181 migrate with the documented day-availability difference/two reject unresolved parents.
Other rejections: duplicate region names 2, invalid leave codes 2, unresolved user rank 1,
dependent leave parents 39. After rollback: one system user, zero crosswalks, zero Events.

Intermediate run `efa24b8a-d7c6-464f-9527-e085dac53767` is retained Failed: 2,230 rows,
156 rejected. It exposed a newly introduced paging bug: Npgsql converted finite year-1
upper-bound seed timestamps to `-infinity`, omitting three roles. Bounds now retain server
timestamp text and cast parameters to PostgreSQL `timestamptz`. Latest adverse run used
BatchSize=2 and transferred all three seed roles across page boundaries.

### Current checks and safety

Fresh source `ss_ready_v4` extends a NEW ready-v3 clone with a distinct January definition
and two duties/shifts/slots. Fresh target `unified_ss_rehearsal_ready_v4` was initialized
from empty, not cloned from an applied target:

| Job | Run | Result |
|---|---|---|
| MyTeam | `155ed7e7-ff4c-4161-b449-0b8f1128f257` | Completed; 41 rows; zero rejected/failed |
| Training | `c52356ca-a408-4ec6-b4b0-89ce2d8d7147` | Completed; 10 rows; zero rejected/failed |
| Scheduling | `df92399a-b504-4a8f-8a80-405888271f85` | Completed; 22 rows; zero rejected/failed |
| Report | `f7a20701-e479-4733-9d32-650312ad1065` | Failed comparison; 73 rows covered; zero rejected/one validation failure |

Catalogue-default follow-up added source Assignment 900020 (no duties) to the positive
fixture and initializer without removing/changing older facts. Singleton defaults now copy
the source definition, not an observed-duty mode: actual target DefaultStartTime=09:00,
DefaultEndTime=17:00. Scheduling `915a6f5d-2774-4cd1-b753-6169af74f1ce` Completed,
23 source rows/zero rejected or failed. Latest Report
`97fd83d5-b47d-4238-9ce5-fb98d519bba6` covered **74** rows, zero rejected, one failed
oracle metric. Earlier 73-row reports remain retained, including
`6f241c0f-b1d5-4f0f-823a-28038c9ac5cc` after source-freshness checks.

Actual January publish preview: two draft shifts/two assignments, CanPublish=true,
zero conflicts. Actual normalized target export: 33 rows; wrong-fixture comparison
22,868 missing/33 unexpected/seven gaps. Source is stable for separately owned actual
SS capture: network `container:unified-ss-migration-source-1`, loopback 5432, database
`ss_ready_v4`, database comment `SS SYNTHETIC`, account `ss_migration_reader`. Configure
the restricted oracle env file securely; do not print its connection value.

Runner-only audit exclusions now suppress source names/email/notes/reasons/SSO/photo.
`redact-synthetic-audit.sql` removed prior private-value copies from the seven exact named
rehearsal targets, preserving business rows, audit identities/actions and run/failure evidence.
Drift-clone run `9f0fb163-cb2c-480e-a140-edb61d5261f8` retained the original drift rejection
and independently inserted a new user/UserRole: 44 rows, one rejected, zero provider failed.
Actual new Unified audit rows exist for both; boolean SQL checks confirm private fields absent.

- SDK10 runner build: **Pass**, 12 existing NU1510 package-pruning warnings, zero errors.
  A dependency-recompiling build also passed with 32 existing package/nullability warnings.
- Focused migration namespace: **71 passed, zero failed/skipped**.
- Dependent Hangfire class: **5 passed, zero failed/skipped**.
- CSharpier format on both migration projects completed; whitespace checked after build/tests.
- Production apply is disabled. Only named isolated synthetic rehearsals with explicit
  `AllowUnrecoverableSyntheticApply=true` can apply while safe rollback is unfinished.
- No shared DB reset, source adverse repair, restore, push, attribution or skipped hook.

Latest command forms (all from this repository; supported SDK10 Compose runner):

```text
rtk docker compose -f docker/migration/compose.yml run --rm --entrypoint sh runner -c 'dotnet tool restore && dotnet csharpier format api/Unified.DataMigration/Services/SsSchedulingWriter.cs && dotnet test --project api/Unified.Tests/Unified.Tests.csproj -- --filter-namespace Unified.Tests.DataMigration && dotnet test --project api/Unified.Tests/Unified.Tests.csproj --no-build -- --filter-class "Unified.Tests.Hangfire.RecurringJobHelperTests" && dotnet build api/Unified.DataMigration.Runner/Unified.DataMigration.Runner.csproj --nologo --verbosity quiet'
rtk docker compose -f docker/migration/compose.yml -f docker/migration/ready-v4.yml run --rm --entrypoint dotnet runner api/Unified.DataMigration.Runner/bin/Debug/net10.0/Unified.DataMigration.Runner.dll queries
rtk docker compose -f docker/migration/compose.yml -f docker/migration/ready-v4.yml run --rm --entrypoint dotnet runner api/Unified.DataMigration.Runner/bin/Debug/net10.0/Unified.DataMigration.Runner.dll Scheduling
rtk docker compose -f docker/migration/compose.yml -f docker/migration/ready-v4.yml run --rm --entrypoint dotnet runner api/Unified.DataMigration.Runner/bin/Debug/net10.0/Unified.DataMigration.Runner.dll Report
rtk git diff --check
rtk proxy git -c core.whitespace=cr-at-eol diff --cached --check
```

Tests/build/queries/Scheduling passed; Report returned nonzero for its real failed comparison.
PowerShell may render Docker's container-status stderr as NativeCommandError even for
successful containers; test summaries/build results and actual runner status above are
the evidence, not that outer-shell formatting artifact.

## Persistent resources

| Resource | State / purpose |
|---|---|
| `ss-synth-pg` | Original container preserved; started, accepting connections, unless-stopped; anonymous data volume retained; no host ports |
| `unified-ss-migration-source-1` | PostgreSQL 12, healthy, unless-stopped, named `unified-ss-migration_source-adverse` volume |
| `unified-ss-migration-target-1` | PostgreSQL 16, healthy, unless-stopped, named `unified-ss-migration_target-isolated` volume |
| `unified-ss-migration_migration` | Docker network; no database host ports published |
| Source database `ss` | Full adverse synthetic fixture, comment exactly `SS SYNTHETIC` |
| Source `ss_ready` / target `unified_ss_rehearsal_ready` | First small smoke rehearsal retained, including its faulty fixture time-expression evidence |
| Source `ss_ready_v2` / target `unified_ss_rehearsal_ready_v2` | Corrected small smoke fixture; not a production-shape or complete edge-case fixture |
| Target `unified_ss_rehearsal` | Adverse dry-run evidence and genuine app reference seed baseline; no applied migration business rows |

Original container contents measured before generation elsewhere: one user, zero shifts,
duties and training histories. It was not overwritten or used as a filled oracle source.

Full adverse source measured: **83 users, 547 training histories, 38,826 shifts,
32,713 duties, 26,917 duty slots, 3,388 manifest rows**. These are fixture counts, not a
production representativeness claim.

Oracle handoff uses `docker/migration/.env.oracle.local` (ignored; never committed) and
Docker network mode `container:unified-ss-migration-source-1`. The env file supplies
`SS_SYNTHETIC_DB` and `SS_SYNTHETIC_ACK`; source role has read-only transactions and SELECT grants.
No secret values belong in this report or command output.

## Adverse MyTeam real-DML dry run

Latest checked run: `244d444d-a599-421e-81f0-be85f373333f` (after final IANA/gender guards).
Result **Failed**, 2,233 selected outcomes, 49 Rejected, zero provider-write Failed,
183 NeedsAction. The earlier post-bootstrap run `8c985003-8804-43cc-b15a-faa099879973`
had the same totals, as did `fe0802be-4a31-4760-abb5-52309e88f6fe`.
Earlier unbootstrapped attempts remain recorded, not erased.

| Entity | Migrated | Excluded | Rejected | NeedsAction |
|---|---:|---:|---:|---:|
| Region | 4 | 0 | 2 | 0 |
| Location | 15 | 0 | 0 | 0 |
| PositionType | 8 | 0 | 0 | 0 |
| LeaveType | 7 | 0 | 2 | 0 |
| Role | 2 | 0 | 1 | 0 |
| User | 81 | 1 | 1 | 0 |
| UserRole | 106 | 0 | 4 | 0 |
| Acting | 8 | 0 | 0 | 0 |
| Leave | 1,660 | 0 | 39 | 0 |
| Away | 0 | 0 | 0 | 183 |
| LookupSortOrder | 0 | 109 | 0 | 0 |

Historical rejections: duplicate exact region names (2), scoped/unknown leave codes (2), missing
standard Sheriff Administrator role (1), unresolved rank (1), and dependent missing
parents (43). The role is not part of the current application default role seed set;
no unauthorized default/app behavior change was made. **The missing-role rejection was
a migration mapper bug, fixed in the latest evidence above; it was not an approved exclusion.**

After rollback, the adverse target has **one seeded system user, zero SS crosswalks,
zero Events**. Reference seeding preceded the checked dry run and is not rolled back.
One early CLI attempt used Audit.NET's default file provider; all 626 generated
synthetic-only file audit artifacts were removed. The CLI now configures the actual
Unified database audit provider, which excludes photo bytes. No such files remain.

## Applied ready-v2 source-to-target checks

| Job | Applied run | Result | Outcomes | Rejected / Failed |
|---|---|---|---:|---:|
| MyTeam | `cbcb01e1-16dd-467a-b221-316829da6f6e` | Completed | 39 | 0 / 0 |
| Training | `ad1e7092-612c-41e3-99b7-b16edffb9158` | Completed | 10 | 0 / 0 |
| Scheduling | `f0caee9c-1fb1-48c2-b96b-dee4a5b3d3e0` | NeedsAction | 13 | 0 / 0 |

Scheduling's 13 outcomes are **12 source rows plus one separate synthetic validation
marker**, not 13 migrated source rows: one room, one definition, four shifts, three duties,
three slots; the marker keeps recurrence/query acceptance visibly incomplete.

Training/Scheduling executed concurrently and share the same MyTeam lineage. Persisted
ready-v2 counts: **2 users (including system), 53 crosswalks, 1 LeaveEntry,
1 UserTraining, 3 ShiftEntries, 3 AssignmentEntries, 3 ShiftAssignmentEntries,
3 ShiftAssignmentEntryUsers**. Seven Events: five Active and two Draft.

Training persisted exact `AwardedOn=EndingOn=2026-11-01T10:00:00Z`, null certificate expiry,
Sent notice and Version 1. Null person-event timezones were not replaced on stored
leave/acting records; event status uses the actual home-location fallback.

Unchanged Training replay `dbabafcb-8f8c-4995-a894-1f03b82e7596` Completed.
Latest checked Scheduling replay `ec73b461-0536-45dc-8199-f5f8051940bd` remained NeedsAction,
13 outcomes, zero rejected/failed. Root/link counts remained stable. A first slot-replay
classification defect was fixed; the prior run remains recorded.

First ready fixture had an SQL timezone-expression precedence error: source duty/slot
instants were 8 hours earlier than the intended shifts. This correctly produced three
dropped missing-shift links. The SQL was corrected with parenthesized local timestamps;
the first databases were retained, not reset or silently relabelled as passing.

The committed ready-fixture initializer also explicitly supplies `Excused=false` for
its Sheriff, avoiding nullable TPH defaults on future builds. Existing rehearsal databases
were not rewritten for this fixture-only follow-up. Source-service capture must identify
the exact fixture/database revision; no repeat-build equality has been established.

## Actual target business queries

The CLI `queries` command called actual query classes against ready-v2 PostgreSQL:

| Window | Actual result |
|---|---|
| 2026-11-01..30 | UserTrainingReportQueryHandler: one Active completion; actual report offset `2026-11-01T03:00:00-07:00`, same UTC instant, null certificate expiry, Version 1, Sent |
| 2026-11-01..30 | WorkingHoursService: one merged shift, 420 credited minutes, 0 OT |
| 2026-11-17..12-15 | WorkingHoursService: two shifts (cutover and next day), each 420 credited minutes, 0 OT |
| 2026-12-01..31 | WorkingHoursService: two shifts, each 420 credited minutes, 0 OT |

The local rehearsal explicitly uses 420 working-day minutes, Leave enabled and LoanOut
disabled. Export refuses an enabled LoanOut path requiring the full scheduling query host.
This limited fixture result **does not establish full source/target report parity**.
Report start was attempted and refused because Scheduling is not Completed.

## Commands and checks

All commands ran from the repository using host RTK with the supported SDK10 Docker image;
host SDK8 was not used for compilation. WSL RTK was unavailable; unrelated tooling was not changed.
`dc` below abbreviates `rtk docker compose -f docker/migration/compose.yml`.

| Command | Result |
|---|---|
| `rtk docker run ... mcr.microsoft.com/dotnet/sdk:10.0 dotnet build api/Unified.DataMigration/Unified.DataMigration.csproj --nologo` | Initial Fail NU1605; migration EF dependency aligned to existing shared 10.0.14; subsequent Pass |
| `dc run --rm --entrypoint dotnet runner build api/Unified.DataMigration/Unified.DataMigration.csproj --nologo --verbosity quiet` | Final Pass; 7 existing package/nullability warnings, zero errors |
| `dc run --rm --entrypoint dotnet runner build api/Unified.DataMigration.Runner/Unified.DataMigration.Runner.csproj --nologo --verbosity quiet` | Final Pass; 27 existing package/nullability/unread-parameter warnings, zero errors |
| `dc run --rm runner schema` (separate adverse/ready targets) | Pass; 57 migrations including migration-owned lineage change |
| `dc run --rm runner bootstrap` (empty isolated targets only) | Pass; actual application reference seeders |
| `dc run --rm --entrypoint dotnet runner api/Unified.DataMigration.Runner/bin/Debug/net10.0/Unified.DataMigration.Runner.dll MyTeam` | Expected nonzero / Failed on intact adverse fixture, as above |
| Above with `-f docker/migration/ready.yml`, job MyTeam/Training/Scheduling | Applied results above; Scheduling exits nonzero for NeedsAction |
| Above with ready overlay, `queries` | Pass; actual query execution, no complete parity verdict |
| `dotnet ef migrations has-pending-model-changes --project db --startup-project db --no-build` inside SDK10 tool container | Pass; no model changes since last migration |
| `dotnet csharpier check` on the two migration projects, migration state/configuration, DbContext, new migration/designer/snapshot | Pass; 35 files checked |
| `dotnet csharpier check api/Unified.DataMigration/Services/SsWriteSession.cs api/Unified.DataMigration/Services/SsMyTeamWriter.cs api/Unified.DataMigration.Runner/Program.cs` inside SDK10 runner | Final Pass; 3 files checked after last guard edits |
| `rtk docker run ... mcr.microsoft.com/dotnet/sdk:10.0 dotnet test --project api/Unified.Tests/Unified.Tests.csproj -- --filter-namespace Unified.Tests.DataMigration` | Fail compilation: two obsolete four-argument job constructors, CS1729; **0 tests executed**, none skipped |
| `rtk proxy git -c core.whitespace=cr-at-eol diff --check` | Pass for Windows CRLF-aware whitespace validation; raw default check flags CRLF in newly added non-CS files |

No whole-database restore, shared app DB reset, push, amended commit, skipped hook or
attribution trailer was used. Full unresolved acceptance work is in
[implementation status](../implementation-status.md).
