# Measured SS rehearsal — 2026-11-16

**Verdict: Fail / NeedsAction.** Ready-fixture inserts are working; full acceptance is not.

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

Rejections: duplicate exact region names (2), scoped/unknown leave codes (2), missing
standard Sheriff Administrator role (1), unresolved rank (1), and dependent missing
parents (43). The role is not part of the current application default role seed set;
no unauthorized default/app behavior change was made to force this case through.

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
