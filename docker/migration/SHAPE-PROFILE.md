# Aggregate-only fitted SS source (T060)

This tooling writes **only new, exactly named SS synthetic databases** on the
separate `shape-source` PG12 service / `source-shape-v7` named volume. It never writes
production, existing/adverse fixtures, Unified targets or acceptance thresholds.
No ports are published. Never use `down -v`, delete failed databases, or
`--remove-orphans` (other rehearsal containers are intentionally retained).

## Retained production-shaped sources — verified 2026-11-17 closeout

These are already loaded; **do not recreate, reseed or reset them**.

| Resource | Exact value / observed state |
|---|---|
| Container | `unified-ss-migration-shape-source-1`, running /healthy |
| Persistent named volume | `unified-ss-migration_source-shape-v7`, local, `/var/lib/postgresql/data` |
| Docker network /DNS | `unified-ss-migration_migration` /`shape-source` |
| Databases | `ss_shape_v10`, `ss_shape_v10_rebuild` |
| Both database comments | `SS SYNTHETIC SHAPE V10` |
| Restart /host ports | `unless-stopped` /none published |
| Actual reader proof, both DBs | all22 public tables SELECT; no INSERT/UPDATE/DELETE/TRUNCATE table grants; read-only on |
| Actual retained counts, each |873 users,115433 shifts,101904 duties |

Safe host-side access uses Docker exec, **not a host-exposed PostgreSQL port**.
No password, full connection string or environment contents need to be printed:

```powershell
rtk docker exec unified-ss-migration-shape-source-1 pg_isready -U ss_migration_reader -d ss_shape_v10
rtk docker exec unified-ss-migration-shape-source-1 pg_isready -U ss_migration_reader -d ss_shape_v10_rebuild
rtk docker exec -e PGOPTIONS=-cdefault_transaction_read_only=on unified-ss-migration-shape-source-1 psql -X -U ss_migration_reader -d ss_shape_v10 -v ON_ERROR_STOP=1 -c "SELECT current_database(), current_setting('transaction_read_only');"
rtk docker exec -e PGOPTIONS=-cdefault_transaction_read_only=on unified-ss-migration-shape-source-1 psql -X -U ss_migration_reader -d ss_shape_v10_rebuild -v ON_ERROR_STOP=1 -c "SELECT current_database(), current_setting('transaction_read_only');"
```

Container-local synthetic trust is the existing isolated Compose policy, **never
production guidance**. For an existing Docker-network client, the service is
`shape-source:5432` on the network above; explicitly select the exact database.
Do not assume the ready-v4/adverse ignored env files select either shape database.
Keep any externally required credentials in existing restricted ignored env/secret
handling; do not put them in CLI values, logs or this runbook.

Exact original SH execution and full comparison proof:
[2026-11-17 original extractor](../../docs/data-migration/reports/2026-11-17-original-sh-execution.md).
Both shape databases pass all comparable/bounded cells, retaining53 noncomparables.
The separate v20 adverse source is intentionally not shape-fit.

## Reproduce

Python3.14 was exercised on Windows. Local-only dependencies:

```powershell
rtk proxy python -m pip install --target docker/migration/artifacts/shape-tools -r docker/migration/shape-requirements.txt
rtk docker compose -f docker/migration/compose.yml -f docker/migration/shape-v7.yml up -d --wait shape-source
```

For **new databases only**, create `ss_shape_v10`, then
`ss_shape_v10_rebuild` using `createdb -U postgres` on
`unified-ss-migration-shape-source-1`. Execute `/fixture/schema.sql` with
`psql -X -q -U postgres -d <exact-db> -v ON_ERROR_STOP=1 -f /fixture/schema.sql`.
Neither initialization nor schema loading is a rerun/repair operation.

Copy the actual source PG12 timezone file before generation (the server is compiled
with `--with-system-tzdata=/usr/share/zoneinfo`). Create the ignored output directory
first; do not substitute the host's newer/different future timezone rules.

```powershell
rtk docker cp unified-ss-migration-shape-source-1:/usr/share/zoneinfo/America/Vancouver docker/migration/artifacts/shape-v10/Vancouver.tzif
rtk proxy python docker/migration/shape_profile.py generate
rtk proxy python docker/migration/shape_profile.py load --database ss_shape_v10
rtk proxy python docker/migration/shape_measure.py --database ss_shape_v10
```

To prove an independent fixed-clock rebuild, **run generate again**, load the fresh
rebuild DB and independently measure it; do not just compare two copies of one DB.

```powershell
rtk proxy python docker/migration/shape_profile.py generate
rtk proxy python docker/migration/shape_profile.py load --database ss_shape_v10_rebuild
rtk proxy python docker/migration/shape_measure.py --database ss_shape_v10_rebuild
rtk proxy python docker/migration/shape_verify.py compare
rtk proxy python docker/migration/shape_verify.py refuse-filled --database ss_shape_v10
```

Loader refuses populated SS/synthetic schemas before mutation. Loaded DBs default to
read-only and expose SELECT-only `ss_migration_reader` on the isolated Docker network.
Generated JSON, full cell comparisons and fingerprints remain ignored under `artifacts/`.
Invented photo bytes are generated inside source `User.Photo` only, not serialized or
hashed. Fingerprints exclude all photo bytes, source Region.Code and
system/audit/permission payloads. Filled-source refusal additionally compares
physical business ctid/xmin receipts without hashing omitted field values.

## Measurement and remaining acceptance

- Input is aggregate-only `ss-implementation-baseline/evidence/ss-shape-extracts.md`.
  All1,565 SH01–08 reference rows /5,906 cells are enumerated, including zeros,
  suppressed bounds and every unexpected actual bucket. `<5` means1..4, not an
  invented exact production count; null suppressed shares/quantiles are explicitly
  NotComparable, never silently passed.
- Independent SQL reads actual stored rows and computes counts, shares, medians,
  percentiles, pattern membership, optional/text-length cells and census. It does not
  read generator counts as measured results. Full failures produce a nonzero exit.
- Generation solves integer workload transports, apportions actual source marginals,
  and exchanges unused real parent schedules while preserving measured marginals.
  Invented unconstrained tails are **not recovered production rows/correlations**.
- Exact original SH SQL was copied unchanged into restricted ignored inputs and
  executed with `psql -X -f` on both shape builds and the separate v20 adverse source.
  Original510-minute edge and rolling last12months definitions are verified without
  substitutions. All5906 production cells are accounted:4631 numeric within ±20%,
  1222 suppressed bounds,53 intentionally withheld. Other EX/AG/AD/SP/OD/LX extractors
  were not executed and are not inferred from SH. No complete business acceptance.
- `ss_shape_v7` is a retained **failed** first fit (24 failed cells plus unresolved
  parent-containment cases). V8 fits all SH cells but retains one zero-duration
  slot outside its parent; it is retained, not repaired. V9 and its independent rebuild
  retain that same one-parent defect. V10 preserves the V9 random seed and corrects
  the joint parent placement using a hybrid full-schedule / hour-duration exchange.
  Failed V10 generation attempts were refused before loading either fresh schema.
  No comparison thresholds changed and no failed loaded source was repaired.
  Original80-sheriff SH08
  failure/adverse source remains unchanged. BL15 target scheduling remains blocked.

Separate adverse-chain commands are in `adverse-v7.yml` / `Invoke-AdverseV7.ps1`.
Use an initialized new `unified_ss_rehearsal_adverse_v7` target. Failed MyTeam must
block dependents; never seed a false completed parent to make downstream cases run.
`adverse_v7_evidence.py` checks actual selected-row and manifest outcome accounting.
