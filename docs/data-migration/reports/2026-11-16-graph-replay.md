# Guarded scheduling graph replay and matched future availability

**Bounded implementation verified; full migration acceptance remains FAIL/open.**
Production apply remains disabled. No target application behavior changed.

## Replay implementation

- Migration-owned connected components contain complete selected Assignment, Shift,
  Duty and DutySlot memberships. Old and proposed component memberships form the
  atomic boundary, so merges and splits cannot commit just one side.
- SHA-256 covers sorted mapped members, including mapped private comments without
  storing/emitting their text. Duty hashes include actual resolved shift crosswalk
  keys; series hashes include resolved definition/child keys and template comment.
  No photos enter these hashes or evidence.
- A rolled-back proposal pass uses the actual writer's hashes. Unchanged definitions,
  shifts, duties and series retain physical roots where their dependencies also
  remain unchanged. A component change alone is **not** permission to rewrite every
  row. No preview business DML survives.
- All old physical roots/aliases require original ownership, hash/version receipts
  and transaction-held root/event/member locks before any replacement. Independent
  target drift or unknown dependents reject the complete family without overwrite.
  Linked-only roots cannot be replaced.
- Changed roots receive generated target IDs; stable primary SS crosswalk IDs are
  rebound. Obsolete synthetic composition receipts alone may be removed, retaining
  encrypted inverse before-images. Every physical Duty segment has an owned
  `DutyEntry` manifest; final aliases are sealed only for newly created owned entries.
- Series removal detaches retained children through the guarded composition path.
  Requalification uses the actual recurrence expander and existing real duties;
  no fictional occurrences are created.
- Nested savepoints restore outcomes/counters correctly. Inner row errors abort the
  entire graph, suppress partial batch commits and persist failure outcomes only
  after rollback. Independent graph units can continue.
- Previously applied older members remain in the selected universe after a later
  history cutoff. Proposals and writes use that same universe. Never-applied older
  source rows are not pulled into normal work merely by lowering an internal bound.

## Actual PostgreSQL executions

Fresh additive source `ss_repartition_v5` cloned immutable `ss_ready_v4`; target
`unified_ss_rehearsal_repartition_v5`. Both older v4 fixtures and their genuine
failures/drift edits remain intact. BatchSize2, actual EF writers, PostgreSQL12
source/PostgreSQL16 target, LoanOut enabled, encrypted inverse journal enabled.

| Case | Run | Result |
|---|---|---|
| MyTeam | `01b97457-d02d-49ee-b205-91aa0ca3f420` | Completed41 |
| Training | `41a9a9c0-6aa0-45f5-82e7-fbe929a669d8` | Completed10 |
| Scheduling baseline | `0d1972da-534b-4c5d-b3e6-704adf2688c4` | Completed23 |
| Unchanged replay | `4ead7b54-b96a-45bb-962b-7abea1114c59` | Completed23; exact footprint unchanged |
| Shift split | `9bc72ab0-8117-487e-b743-dcccc67561da` | Completed23 |
| Shift merge | `831b3e9d-cac1-4ad5-aee6-b4c03881cde9` | Completed23 |
| Definition merge | `dfd70d1d-b308-472b-8893-98dd84409ca9` | Completed23 |
| Definition split | `a62b40b6-46d7-4213-826a-b5813cca4157` | Completed23 |
| Series becomes unqualified | `cf82dc29-cb7c-4140-a3b1-57afd3fc8747` | Completed23; standalone real duties |
| Series requalifies | `4cd3c645-6326-41a0-b3ca-80f44895afec` | Completed23; exactly two real children |
| Second synthetic user | `b1979934-a056-43be-9782-2e8dd0aaaa8d` | MyTeam Completed42 |
| Multi-entry Duty add | `a7643c7a-e7bc-49fa-9dd5-d47783e3e3bd` | Completed25; two owned segments |
| Multi-entry interval change | `52493c1b-7320-4629-b3f0-947cb06c9d93` | Completed25; second segment ends14Z |
| Second slot expires | `1d22bd3c-8eae-44d1-99f9-ada257c36a46` | Completed25; obsolete segment/event removed |
| Add actual Wednesday occurrence | `e879cbaf-2cfb-4cf0-8d66-34cdcacb1299` | Completed28; actual duties900010/11/12 only |
| Remove Tuesday qualification | `1c052d7e-8a68-4bc9-af46-ca1d7811704d` | Completed28; series900010/12, 900011 standalone |
| Unchanged final replay | `7421baa2-9f46-4bb8-a505-616bd15f363b` | Completed28; exact footprint unchanged |
| Injected SQLSTATE40001 | `f11d11af-42f8-44d5-aa2e-9e6077c6b81f` | Genuine Failed; 15 failed primary outcomes; footprint unchanged |
| Retry after removing injector | `18bc2300-9500-4ad0-b84b-2f0a96c0960f` | Completed28; split replay succeeds |
| Independent target edit + source merge | `dc32d952-056d-4902-9db1-5b59d00a3d9d` | Genuine Failed; 15 rejected, no overwrite |

Each successful topology case asserted crosswalk target existence, owned physical
Duty segments, event/series footprint and exact membership. Retention assertions
checked unchanged mapped-source roots keep IDs and unchanged definitions/shifts keep
complete physical rows (including events/users), not just counts. The retained target
deliberately still contains its independent edit; source's rejected merge is unapplied.

Reproducible operational cases: `docker/migration/Invoke-RepartitionV5.ps1`, with
`verify-repartition-v4.sql` and `graph-proof-v4.sql`. The default mutation sequence
requires a freshly initialized v5 fixture; do not rerun it on a completed fixture.
`-Adverse` follows the successful sequence once; `-VerifyOnly` checks the retained
final split fixture without modifying business rows. Proof tables contain only
keys/digests, never private text or photos. These are **operational PG assertions,
not new xUnit coverage**.

### Earlier history and confirmed graph inverse

Separate additive source/target `ss_repartition_history_v5` /
`unified_ss_rehearsal_repartition_history_v5`; unused identity910020 is a synthetic
2014 Assignment/Duty/Shift/Slot. Initial seed attempt used already-existing900020
and failed before any write; corrected unused identity, no existing row overwritten.

- Normal parent/training/scheduling: `b2c97ab0-ab1b-4259-9d5d-93240fc717cd`,
  `b2d0488c-1ef4-4478-a2e2-4b6cd768854e`,
  `302d8cd1-2cf8-4841-b104-5e540c82ec98`: Completed41/10/27.
- 2014 parent and Scheduling backfill: `0a335f06-7dfb-4f7b-bb6b-98059a009891`,
  `b1aec162-1c72-4531-909d-264f8cc0dcfb`: Completed41/27.
- Return to2015: `af1db536-5fbe-40f0-9ba9-2cce78f32dfb`, Completed27;
  complete scheduling footprint equals the earlier-backfill footprint.
- Change known older shift, add never-applied older touching shift910021:
  `1a0ca5c0-fb5a-48f5-87c3-423aadd19cf4`, Completed28. Actual old shift now ends17Z;
  new older member has no target crosswalk, remaining outside normal selection.
- Preview of that replacement: **78 rows, zero extra dependents, zero blockers**.
  Immutable restricted impact digest:
  `B849D1CBAB38AD0D19F28EE6F0395F265680FCD20E554FD50BEEEFF942555D45`.
  ConfirmDataLoss was exactly run ID + digest; confirmed receipt
  `a222efb9-9576-4edf-811f-9107ae96fe8e`.
  Atomic inverse restored the **exact prior complete scheduling footprint**.
  Source edits remain untouched. Broader multi-segment/series inverse coverage is
  still needed; this is not a universal rollback acceptance claim.
- Final first-apply proof capture is bound to primary member outcomes, not an
  ignorable synthetic metric. Post-inverse replay/new standalone catalogue component:
  `6056615d-3caa-46e7-99b4-8c7be272f337`, Completed29.
  Injected40001 on a new SchedulingMember receipt produced genuine primary Failed1:
  `c875897b-5c57-4bbd-9a6b-53d37dab1a50`, Scanned30. Removing the injector recovered:
  `ce27df05-8b6c-40d6-b905-38aaf4f2959a`, Completed30. No trigger remains.

## BL15: matched future actual-service result

Independent artifact `probe-v4-availability-v1-e6e674c.json`,12 actual SS observations,
SHA256 `874D6B412686D940517685C0A3D4BF73F5A3D866AE11B19E55CF70D0051EB9F6`;
guarded exact source `ss_ready_v4_probe`, target `unified_ss_rehearsal_ready_v4_probe`.
Source owner reports read-only capture/fresh replay and unchanged earlier fingerprints.
This process executed the matching actual Unified services with LoanOut enabled.

Restricted ignored target output `target-future-matched-availability-probe-v4-v1.json`,
SHA256 `F32618D5AC5E0B264F1458BBD113D72568FAC340D20376235B0C790DA34FAAA7`.
**Verdict Fail: 12 calls, two candidate-presence differences, four conflict-count
and four conflict-interval differences; SS four Away instances, target engine zero.**

Exact intervals UTC on December10,2026:

| Window | SS home/destination presence; conflicts | Target resource union; proposed-shift conflicts |
|---|---|---|
| Before08–10 | Both present; zero | Both present; zero |
| Inside10–12 | Both present; one Away each | Both present; zero |
| After12–next-day08 | Both present; zero | Both present; zero |
| Whole day08–next-day08 | Both present; one Away each | Both present; zero |
| Strict before08–09:59:59 | Home present/destination absent; zero | Both present; zero |
| Strict after12:00:01–next-day08 | Home present/destination absent; zero | Both present; zero |

Every SS returned Away interval is **10–12UTC, timezone null**, not the whole query
day. Target precise home LoanOut calendar returns that exact interval for inside/
whole-day calls and empty elsewhere; destination calendar empty. Target resolver
uses Etc/UTC. Target explicit supplied-candidate guard rejects home6/6 and allows
destination6/6; source capture did **not** execute equivalent save-validation calls.

Primary API-level impact is concrete: destination date-only discovery exposes a
candidate outside the SS instant window, and target proposed-shift conflict responses
omit all four observed SS Away conflicts. Exact calendar timing alone is insufficient.
Home guard suppression outside10–12 is a separate actual target restriction, not a
proved source write/UI/free-minute loss. No source status/conflict formulas were
reconstructed and no target app rule was added. **Keep BL15 future staffing acceptance
Fail/NeedsAction pending an explicit Scheduling/LoanOut decision**, not an automatic waiver.

## Checks and boundaries

- SDK10 runner build: PASS (six existing NU1510 warnings); intermediate signature
  compilation failure corrected before the successful cases above.
- Final focused migration/Hangfire tests: **PASS76, failed0, skipped0**.
  A nested-shell filter quoting attempt failed before test execution; corrected
  direct dotnet invocation above executed all76. An intermediate PS5 line-break
  parse failure was also corrected; final `-VerifyOnly` passed with SHA-256 proofs.
- Actual PG scenarios above: PASS, including expected genuine failure assertions;
  inverse preview/apply: PASS.
- Actual future availability comparison: expected genuine FAIL/exit1, not a passing
  acceptance test. Artifact is retained; no restricted rows committed.
- Final CSharpier/Compose/PowerShell parse/whitespace results recorded in status.
- CSharpier check all10 changed C# files: PASS after correcting executor formatting.
  Compose `config --quiet` for v5 and history-v5: PASS; PowerShell parse and actual
  `-VerifyOnly`: PASS. Future output hash, owner-only ACL and ignored private paths:
  PASS. Required raw `rtk git diff --check` exits2 on repository CRLF formatting;
  `rtk proxy git -c core.whitespace=cr-at-eol diff --check`: PASS.

Remaining outside this bounded implementation: isolated dry-run parent staging;
SH01–07 and ±20% production-shape fit (SH08 still fails24/26); broader independent
PG/concurrency/inverse coverage; full adverse target rehearsal; unresolved actual
service/business parity. Source full-snapshot reconstruction remains a performance
note, not missing durable target resume/retry correctness.
No application behavior/GitOps changes, shared DB reset, attribution or push.
Original contract-file deletion remains unstaged.
Parent-owned canonical workspace documents were not edited/refreshed in this pass;
the existing repo-local baseline mirrors are historical. The linked strict-row
classification and future comparison provide exact input for the parent's refresh.
