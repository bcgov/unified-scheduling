# SS Migration Validation Evidence

**Session started 2026-11-16. Initial implementation runs; acceptance is Fail / NeedsAction.**

## Planning Checks

| Check | Evidence / result |
|---|---|
| Active feature | `.specify/feature.json` points to `specs/001-data-migration-jobs` |
| Existing app branch | `Feature/SS-872-ss-migration-job`; no branch created/switched |
| Initial worktree | Pre-existing deleted `docs/data-migration/ss-mapping-contracts.md`; preserve and do not stage |
| Workspace Git ownership | Workspace specs/documentation are outside a Git repo; versioned mirror needed for requested milestone history |
| Plan hooks | `.specify` has no extensions.yml; no hooks apply |
| Setup/prerequisites | Initial RTK/Bash wrapper failed; native Git Bash via argument array passed setup-plan and check-prerequisites --require-tasks --include-tasks; existing plan preserved by setup |
| Checklist gate | requirements.md: 16 total / 16 checked / 0 unchecked, PASS; markers not edited |
| Document links/backlog | Local-link check: 0 missing paths; BL-1..6 all transferred |
| Source findings | Final worksheet through G-19/BL-6, split/cutover/report/dry-run/rollback design, production SH extract read |
| Runtime/tooling | Prior inspection: host SDK8; target needs .NET10 container. Existing `ss-synth-pg` stopped; persistence/contents not yet verified |

## Runtime Checks

Pending implementation evidence. Record exact commands, counts and artifact paths here.
Do not promote old fixture checks, code inspection or unit-test success to migration validity.

| Gate | Current result |
|---|---|
| Persistent source schema/health | Healthy isolated PostgreSQL12 source/PostgreSQL16 target; original source preserved and accepting connections |
| Production-shape fit +/-20% | Fail: SH-08 has 24 failing buckets; SH-01..07/full fit pending |
| Actual SS report oracle | Adverse source: 29 tests/0 skipped. Matched ready-v4 source: 33 tests/0 skipped; training/full-report/availability replay hashes match. Target parity still pending |
| MyTeam identity/auth-field persistence | Ready-v4 Completed, 41 source rows; role create/link and actual LoanOut away mapping fixed. Real login unproved |
| Training exact facts/report comparison | One ready completion persisted with equal start/end, null certificate expiry, Sent and Version1; actual target report reads same instant. Full source/target parity pending |
| Scheduling intervals/links/publish parity | Ready-v4 Completed, 23 source rows/0 rejected/failed; actual series/LoanOut and publish preview exercised. Full matched oracle parity pending |
| Full outcome coverage / accepted verdict | Latest Report covers all 74 ready source rows but Failed comparison: independent capture from different fixture plus unsupported surfaces |
| Replay/conflicts/retry/backfill | Durable bounded target batches, interruption/resume, below-cursor retry and 2014 backfill now exercised; broad mapped updates implemented. Multi-root repartition/series changes still pending |
| Dry-run business rollback | Adverse MyTeam real DML rolled back: one system user, zero Events/live crosswalks after run |
| Confirmed rollback/footprint proof | Encrypted inverse journal, confirmed digest and dependency guards implemented; PG inverses/reapplication exercised. Full rollback/footprint coverage pending |
| Build/tests/EF/format | SDK10 build/EF/CSharpier/Compose pass. Obsolete constructors fixed: 71 migration + 5 Hangfire tests pass, zero skipped. Broader PG coverage pending |

Use SS-V001..057 from the [stable validation register](../../documentation/data-migration/ss-job-notes/validation-tests.md).
SS-V040/041 are superseded, not unimplemented ownership work.
SS-V036's old app-default edit is now BL-9; migration-only explicit-false preflight remains required.

## Oracle Milestone

Legacy commit `beea0fbc91545cd87c754c47736047c9b07c1951`, not pushed.
[Harness](../../legacy/sheriff-scheduling/test-harness/report-oracle/README.md):
actual-service training/export comparison, optional shift/duty/distribution/overtime capture,
fixed windows, synthetic marker/environment guard and read-only transaction.
SDK5 build passed; `Invoke-Oracle.ps1 -Mode TestUnit`: 20 passed, zero skipped.
Unconfigured CLI/PostgreSQL cases fail closed. No ordinary application files changed.
Initial results proved harness unit behavior only. Follow-up source execution is below.

### Actual Source PostgreSQL Follow-Up

Legacy commits `e2da417` / `9a57c54`, not pushed. 29 tests passed, zero skipped, including
two PostgreSQL tests. Actual SS service captures and fresh replay matched. Synthetic database
marker/read-only transactions and current-user-only artifact ACLs verified; no CHES/JC/PDF
effects or source mutation. The source schema/data remain owned by the mock harness.

| Captured rows | Past month | Cutover +/-14 days | First future month |
|---|---:|---:|---:|
| Training API | 1,102 | 1,102 | 1,102 |
| Non-Excused projection | 1,070 | 1,070 | 1,070 |
| Shifts | 1,533 | 916 | 258 |
| Duties | 1,053 | 998 | 1,008 |
| Duty slots | 1,017 | 624 | 186 |
| Distribution conflicts | 1,772 | 1,048 | 387 |
| Overtime calculations | 1,469 | 875 | 249 |

Restricted/ignored `test-harness/report-oracle/artifacts/` contains
`source-training-v1-9a57c54.json` (6,516 rows), `source-all-v1-9a57c54.json` (22,868),
and `source-summary-v1-9a57c54.json` (counts, hashes, guards).
The Non-Excused projection is derived by the harness, **not executed UI/CSV evidence**.
[Target envelope](../../legacy/sheriff-scheduling/test-harness/report-oracle/TARGET-EXPORT.md)
requires independently executed target services and source-ID crosswalk normalization.
No target parity follows from a source-only pass.

## Initial Unified Runtime Milestone

Local commits `b589bee4` -> `e16b559b` -> `7287236` -> `21ae1f3`, not pushed.
[Measured report](../../unified-scheduling/docs/data-migration/reports/2026-11-16-rehearsal.md)
records exact commands/run IDs. Initial implementation is explicitly incomplete, not a pass.
Source `unified-ss-migration-source-1` and target `unified-ss-migration-target-1` are healthy,
unless-stopped, no host DB ports. Existing `ss-synth-pg` and anonymous volume retained.

Adverse fixture: 83 users, 547 training histories, 38,826 shifts, 32,713 duties,
26,917 slots, 3,388 manifest rows. These are **not production-shape-fit evidence**.
Adverse MyTeam dry run: 2,233 outcomes, 49 Rejected, 183 NeedsAction, zero provider-write
failures; business rows rolled back. Duplicate-region/unknown/scoped-type cases are genuine
adverse data, but missing Sheriff Administrator creation is a mapper defect, not approved
rejection. Fix and rerun required.

Ready-v2 MyTeam/Training Completed; Scheduling NeedsAction, with 12 source-row outcomes plus
one separate validation marker. It holds three ShiftEntries, three AssignmentEntries,
three staff links and seven Events (five Active/two Draft). Unchanged Training replay stable.
Actual target report/WorkingHoursService queries execute, but limited fixture/disabled LoanOut
and missing source capture **do not establish full workflow parity**.

### Second Implementation Milestone

Commits `67982bc2`, `5bd55720`, `edb9c393`, `e29fdd87`, `35dc8164`, not pushed.
Role creation bug fixed; 181/183 adverse away rows now map through current LoanOut, two
reject unresolved parent users. Corrected adverse dry run: 2,233 rows, 48 Rejected, zero
provider Failed and no blanket away placeholder; real business writes roll back.

Ready-v4 MyTeam/Training/Scheduling Completed with 41/10/23 source rows, no rejected/failed.
Actual LoanOut availability/calendar, qualified recurrence, source singleton default times,
publication preview and isolated-clone publication exercised. Selected owned UserRole update
retains target ID; later external text edit blocks the next source update without overwriting.
Latest Report covers 74 rows but fails the wrong-fixture capture and unsupported comparison
surfaces. Matched ready-v4 SS capture requested; this is coordination, not evidence of parity.

Migration test compilation is repaired: 71 migration and 5 Hangfire tests pass, zero skipped.
These are not a substitute for missing full PostgreSQL retry/backfill/rollback coverage.
Production apply remains disabled while recovery/progress implementation is unfinished.

### Durable Recovery Follow-Up

Commits `40fd1b9` and `dd86557`, not pushed. SDK10 build and 76 focused tests passed,
zero skipped. Real PostgreSQL interruption retained 14 prior batches and rolled back the
active batch; restart, below-cursor dependency retry and separate 2014 backfill succeeded.
Mapped text-only updates, new touching shift member, profile preservation and independent
target training-note drift rejection were exercised on separate probes.

Source snapshots still rescan for full group/dependency reconstruction; this is a performance
limitation, not an assertion that durable target progress is absent. Multi-root repartition,
changed series qualification/membership, dry-run parent staging and full confirmed rollback
remain incomplete.

Ready-v4 oracle handoff initially failed its exact-database guard: the separately named
restricted env file still selected `ss`. Commit `0108ca5` corrects its actual connection
property; the owner independently reparsed it with Npgsql and confirmed `ss_ready_v4`.
The original adverse config is unchanged. SDK10 build and 76 tests passed, zero skipped.
Corrected metadata is now relayed for matched capture; no matched result is recorded yet.
Actual target partial-day availability probes show 1,320 suppressed user-minutes outside each
two-hour interval. This alone **does not prove an SS difference**; actual SS availability
comparison was requested before declaring BL-15 a confirmed primary-goal blocker.

The matched source probe uses home location `900000`, destination `900001`, sheriff
`11111111-1111-4111-8111-111111111111` and away row `900000` in `ss_ready_v4`.
Source timezone is null; target fallback is `America/Vancouver`. UTC windows are
2026-11-03 08:00-10:00, 10:00-12:00 and 12:00 through 2026-11-04 08:00.
Both locations must be queried through actual SS services before classifying a difference.

### Matched Ready-v4 Source Oracle

Legacy commit `e6e674c13168dbd606f71793ec5c71c62792abb1`, not pushed.
33 tests passed, zero skipped; exact database `ss_ready_v4`, synthetic marker, read-only
transaction and private artifact ACLs verified. Fresh training/full-report/availability
replays matched; original adverse artifact hashes are unchanged.

Restricted oracle artifacts:

| File | Result |
|---|---|
| `ready-v4-source-training-v1-80e9aae.json` | 6 rows |
| `ready-v4-source-all-v1-80e9aae.json` | 57 rows; SHA-256 `DC28370841D1B444D3950E897EDEE2AA268ACC1EDAD7DFDCCDEECA2B1EDC62C7` |
| `ready-v4-availability-v1-e6e674c.json` | 12 actual service calls; SHA-256 `002C5DB06A157EB1A79413608E66C0E437FFC362C1DB194B4D8E90DF1BB88D24` |
| `ready-v4-source-summary-v1-e6e674c.json` | Counts, hashes and safety results |

Actual SS home-location probes return no conflict before/after, one Away conflict inside.
The whole-day response retains only the original 10:00-12:00 UTC interval, timezone null.
Destination strict-outside probes omit the sheriff; boundary-touch probes return the sheriff
without conflicts. Raw SS service results therefore do not support whole-day home suppression.
This is not a free-minute calculation, UI comparison or accepted target parity. Exact matched
target comparison is requested; the future `ss_ready_v4_probe` source run awaits its own
restricted connection file.

### Rollback Journal And Shape Checks

Unified commits `5ecbf206` and `d5f832e9`, not pushed. Owner reports SDK10 build,
76 focused tests (zero skipped), whitespace checks and PostgreSQL inverse/reapplication
probes passed. Encrypted rollback journal, confirmed impact digest, dependency guards,
Duty text/dependency replay and actual availability export are implemented. Current
MyTeam/Training/Scheduling loaders complete with zero pending retries. This does not prove
all rollback ownership, conflict, dependency-race or failure-atomicity cases.

SH-08 has 24 failing buckets: the fixture is not production-fit. SH-01..07/full fit,
repartition, multi-entry Duty updates, changed-series replay, dry-run lineage candidates
and matched target Report remain unfinished. Production apply stays disabled.
