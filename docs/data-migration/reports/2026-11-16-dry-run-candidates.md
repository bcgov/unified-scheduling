# T065 — real-DML dry-run parent candidates

**Simulation only. Full migration/business acceptance remains open and production
apply remains disabled.** No application auth/scheduling/training behavior changed.

## Implementation and safety boundary

- Durable migration-owned `DataMigrationSimulations` and
  `DataMigrationSimulationCandidates` are a separate simulation namespace, **not**
  applied `DataMigrationRecords`. Candidate rows contain source/entity/legacy IDs,
  generated target keys and SHA-256 receipts only; no mapped personal text, credentials,
  photos, source audit/keyring payloads or serialized business snapshots.
- MyTeam executes the real writers and PostgreSQL DML/constraints against a private
  transaction-created schema. Training/Scheduling reconstruct that exact parent
  from the read-only source snapshot and fixed target baseline, execute the same
  writers, and verify the durable candidate identity/hash receipt before proceeding.
  Complete source, target baseline/schema, code/config, history, cutover, namespace,
  parent job/status/DryRun and candidate-row receipts must match.
- Public business tables are locked SHARE while their baseline is copied. Ordinary
  public rows are never written. Private sequences are seeded independently; generated
  User GUIDs are namespace/legacy-key based. No applied SS crosswalk/ownership receipt,
  mutation, retry or checkpoint is loaded into the simulated crosswalk namespace.
  Existing target business rows are copied as baseline and retain normal link-only
  semantics; this is **not a confirmation/preview of updates to applied SS ownership**.
- Cloned PK/unique/check constraints and FKs enforce real DML; every cloned FK points
  inside the rollback schema. `search_path` has no public fallback; explicit migration
  row-version reads also use the private schema. Unknown business triggers, external
  FKs, RLS or explicit schemas fail closed. Optional ETL tables are outside the SS
  simulation surface. The obsolete convention `UserLeave` model has no physical table;
  it is not simulated or repaired. Current SS leave uses actual `LeaveEntries`.
- Every private table, candidate business row, business audit and sequence is rolled
  back together. Only afterward, under a rechecked public fence/control transaction,
  successful run/outcome and candidate metadata are committed atomically. Failed or
  NeedsAction jobs publish no candidate manifest. Run/outcome **metadata audit**
  remains durable; simulated business audits do not escape rollback. New staging
  metadata is AuditIgnore (also avoids the application's single-PK audit assumption
  for the migration-only composite candidate key); no audit app code was changed.
- Repeats preserve candidate identities and mapped source receipts, **not new-run audit
  timestamps**. Source photo bytes never participate in hashes/storage; source photo
  presence/change timestamps and target User row versions provide the private-photo
  invalidation guard. Parent reconstruction uses the original parent run timestamp.
- Dry Report reconstructs MyTeam + both matching children and reconciles actual private
  targets, but is deliberately **NeedsAction / SimulationOnlyNotAppliedLineage**.
  It never invokes an applied-target oracle export, publish or acceptance assertion.

## Actual PostgreSQL evidence

Dedicated additive source `ss_simulation_v6` cloned immutable `ss_ready_v4`, marked
`SS SYNTHETIC`; target `unified_ss_rehearsal_simulation_v6`. Older sources/targets,
adverse edits and original `ss-synth-pg` were not reset. LoanOut enabled, BatchSize2.
Schema migrations added migration metadata only.

Operational suite: `docker/migration/Invoke-SimulationV6.ps1` with
`simulation-v6.yml`, `ready-v4.yml`, `rollback-ready-v4.yml` and base Compose.
The suite seeds a deliberately **applied** SS Region/crosswalk sentinel unrelated to
the selected source. Before/after assertions cover **all ordinary public row physical
identities/versions, all applied crosswalks/mutations/progress and all business
sequences**. Only run/outcome/candidate metadata and its audit are permitted to persist.
It additionally rejects any escaped business audit or leftover `ss_sim_*` schema.
Proofs hash physical metadata, not private text/photo bytes. Deliberate target-staleness
test mutation is independently snapshotted before the rejection, then explicitly restored.

First complete v2 suite (all ordinary-footprint assertions passed):

| Case | Run(s) | Actual result |
|---|---|---|
| MyTeam real DML + stage | `2c89ccd2-039c-4cf9-bbe1-b14713e49bae` | Completed41 |
| Training/Scheduling resolve parent | `389261e8-a8de-4197-8b0c-35b11edd6cc0`, `9feea6df-8cbf-460c-af97-986576992ff3` | Completed10/23 |
| Repeat dependent jobs | `303b09f0-01da-4b64-884d-8e3edf2f3490`, `1d775a23-6243-4180-a605-e8da42e513e3` | Completed10/23; same receipts/keys |
| Dry Report | `835a4a76-8a97-4120-b0ba-c6a2455fab2d` | NeedsAction74, not applied acceptance |
| Repeat MyTeam + dependent chain | `1094657a-d266-4398-afd0-aec748063362`, `8f519b2b-5917-487f-b1f2-db1b55979a4a`, `c2937d4a-70e1-4544-997d-d1efc48d6926` | Completed41/10/23; same namespace/receipts |
| Wrong candidate namespace | `653f0e6c-a3db-47c3-8f89-20ea91f33eb3` | Genuine Failed/Rejected1; no business writes |
| Wrong parent lineage | `b6885c77-f523-40d6-b851-9bf31681c135` | Genuine Failed/Rejected1 |
| Stale source snapshot | `eee399f8-cfb6-4316-b08c-59ff5962dce2` | Genuine Failed/Rejected1 |
| Stale target baseline | `2667525b-2175-4eb9-b4f3-7a0ffd89f917` | Genuine Failed/Rejected1; independent edit preserved |
| Recovery after restoring probes | `22d75b94-ed65-46d0-ab86-6bedea3ab8e9`, `2b6c1d44-6aa2-4737-b9c9-b8ee5be2fb92`, `6df8074a-41f5-49b6-99ba-4229e4357598`, `b15377ee-2bce-463b-b56b-cbfeb6d80e92` | Completed10/23/10/23 |
| Final dry Report | `1e7d0b3e-dbc6-4859-91d4-fae035f6068b` | NeedsAction74; unchanged public footprint |

These are real PG operational assertions, **not additional xUnit tests**. The suite
was then repeated after final formatting/DDL-helper cleanup; final results are in the
milestone validation record below. Candidate identity assertions compare like code/
configuration versions; a rebuild/version change intentionally invalidates old staging.

### Final-source rerun and validation record

The final DDL-helper/diagnostic cleanup built successfully in the SDK10 container:
zero errors, six existing NU1510 package-pruning warnings. Full actual-PG suite then
passed again, including all expected failures, restoration/retry and footprint proofs:

- Initial MyTeam `e301f70f-d35a-4c4e-bfa7-840953abbef7`, Training
  `9e3b59dc-885d-45fc-935c-4d3090780699`, Scheduling
  `ae4d63e5-c82d-4c8d-8516-9aa0c5d27c72`: Completed41/10/23.
- Repeated root `8ea65ea3-c3c8-40de-abe5-d7ba4353d291`, Training
  `41ed7ed7-a437-4cef-aaba-58196c828015`, Scheduling
  `3e08c630-8dae-45fb-b1ba-fee235de135f`: Completed41/10/23.
- Both roots and all successful child repeats share namespace
  `115708bd-3448-b1a6-1b92-8266beb2f1ce`. Durable candidate counts
  **33 MyTeam /10 Training /56 Scheduling** are identities, not selected source counts.
  Final successful child receipts equal earlier repeats in that namespace.
- Wrong candidate namespace `53dd4368-da8a-49d4-a306-06d6105107f8`, wrong parent
  `756fd7dc-e13b-4c84-ad16-d13b88a03480`, stale source
  `98c81cb3-af75-45e1-8546-706cee703ec2`, stale target
  `5bde7109-c9c6-4da1-9b5d-6b7d7857b5e1`: all genuine Failed/Rejected1.
  Expected reasons checked in PostgreSQL, no business effects.
- Restored-case retries `ab1aedf6-aa35-4e90-80a0-49a1309e6d7b`,
  `7935bb8d-a05f-4ccd-8cd7-0a751527e43f`,
  `df0b6e0f-66de-4c45-9401-ddf784d7fb36`,
  `7094ab29-8c77-407c-ab8f-47d6388dda26`: Completed10/23/10/23.
- Final Report `7f714685-95ee-4574-85c0-179de09eeb70`: **NeedsAction74**,
  Rejected0/Failed0, explicit `SimulationOnlyNotAppliedLineage`. Not a pass.
  Exactly one original applied sentinel crosswalk remains; leaked schemas **zero**.
- `dotnet build api/Unified.DataMigration.Runner/Unified.DataMigration.Runner.csproj
  --no-restore --verbosity quiet`: **PASS**, SDK10 Docker.
- `powershell.exe -NoProfile -File docker/migration/Invoke-SimulationV6.ps1`:
  **PASS**, actual PG12 source/PG16 target, LoanOut enabled.
- `dotnet test api/Unified.Tests/Unified.Tests.csproj --no-restore --filter
  'FullyQualifiedName~DataMigration|FullyQualifiedName~Hangfire' --verbosity quiet`:
  **PASS76, failed0, skipped0** in SDK10 Docker. Existing unrelated compile/package
  warnings retained; no test suppression, hardcoded pass or test edits.
- CSharpier check all10 changed handwritten C# files: **PASS**.
  Compose merged-overlay `config --quiet` and PowerShell AST parse: **PASS**.
- Whitespace validation initially caught/fixed a newly appended blank EOF.
  Required `rtk git diff --check` / native equivalent still return2 for existing
  CRLF line endings in edited Markdown; explicit
  `rtk proxy git -c core.whitespace=cr-at-eol diff --check`: **PASS**.
  No global Git whitespace configuration was changed.

### Retained implementation failures

Initial attempts failed rather than hiding missing candidates: unrelated ETL model
mapping, obsolete `UserLeave`, a PL/pgSQL record/table-alias collision, then explicit
public-only version reads were resolved in migration code. The composite candidate
audit assumption failed before metadata commit; migration-only AuditIgnore fixed it.
An initial proof helper used `to_jsonb(sequence)` (unsupported by PG); it was corrected
to explicit sequence metadata before the passing suite. Earlier failed runs remain.
An initial DB build command named the wrong csproj; corrected to `db/db.csproj`.
First migration was generated by EF10.0.0 against10.0.14 (warning); candidate-links
migration used matching EF10.0.14. Both generated migrations change migration tables only.

## Operational limits and remaining acceptance

- Requires target CREATE SCHEMA and baseline SELECT/SHARE-lock rights. No rights or
  deployment configuration were granted/changed. If the eventual migration account
  lacks them, provisioning belongs to Unified-DevOps-Lead via Unified-Architect.
- Baseline copying/full source re-reading is intentionally conservative and may be
  expensive. No durable private business/photo copy is retained; source or baseline
  changes require a fresh successful MyTeam simulation. No TTL/clock-based cutover.
- Applied ownership/update preview, broader independent concurrency/cancellation and
  drift/rollback coverage still need validation; simulation is not apply authorization.
- SH01–07/full ±20% fit and full adverse target rehearsal remain required. SH08 still
  fails24/26 comparisons. BL15 is now a **measured primary future scheduling blocker**;
  see [complete matched differences/proposed app decision](2026-11-16-matched-differences.md).
- Parent-owned root documents are not edited here. Final canonical mirror refresh is
  still required after those documents settle; do not call historical mirrors current.
  Original deleted contract file remains unstaged. No attribution or push.
