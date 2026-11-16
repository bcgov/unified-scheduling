# Aggregate-only fitted SS source (T060)

This tooling writes **only new, exactly named SS synthetic databases** on the
separate `shape-source` PG12 service / `source-shape-v7` named volume. It never writes
production, existing/adverse fixtures, Unified targets or acceptance thresholds.
No ports are published. Never use `down -v`, delete failed databases, or
`--remove-orphans` (other rehearsal containers are intentionally retained).

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
- The original external SH/EX/AG/AD/SP/OD extractor SQL is not supplied in this repo.
  The independent implementation is named as such in evidence. Exact external
  extractor execution remains unverified, particularly original duration-band edge
  and last12months window definitions. Do not equate local-fit evidence with completed
  T060 acceptance until those files are provided and executed.
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
