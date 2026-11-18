# Exact original SH01–08 execution — 2026-11-17

**Original-query verification completed. No substitute SQL, source mutation,
clock substitution or acceptance-threshold change. Production remains disabled.**
This closes the exact SH extractor verification gap, not full migration/business
acceptance. BL15 and all unwaived adverse/Report differences remain unchanged.

## Unchanged inputs and execution

Copied the existing workspace files directly, without waiting for a handoff, into
restricted ignored `docker/migration/inputs/original-extractors/`.
Native SHA256 checks proved source/copy equality for both files:

| Input | SHA256 |
|---|---|
| `ss-shape-extractor.sql` | `70C0BB834026A117BF32F8D1FAD7A40F7BC4039BE9BEBFBEE8B8CCC023232920` |
| Original production `ss-shape-extracts.md` | `C06BD03770951358B619A7E2AF36C0DAE70A9BAA111497AE134DF75C8FCA0F40` |

`docker cp` transferred these exact SQL bytes to each source container at
`/tmp/ss-shape-extractor-<lowercase SQL SHA256>.sql`; container `sha256sum` matched.
The original file itself, not stdin or a replacement SELECT, ran with:

```text
rtk proxy docker exec -e "PGOPTIONS=-c default_transaction_read_only=on" <container>
  psql -X -U postgres -d <exact-db> -v ON_ERROR_STOP=1 -f <exact-container-file>
```

Each connection first checked the exact database name/comment and read-only state.
The file independently starts one REPEATABLE READ READ ONLY transaction and ends
with ROLLBACK. Its own SH00 output confirms `Transaction read only | on`, PG12.22,
and EF migration `20250723232143_SeedDataForSheriffStandardTraining`. All three
psql executions exited **0**, with empty stderr. No original query error or fixture
schema defect occurred.

Only the PGOPTIONS read-only safety setting was supplied. There were **no SQL,
parameter, timezone-rule or clock substitutions**. Original semantics retained:
2015-01-01T00Z inclusive start cutoff; unknown/missing timezone fallback to
America/Vancouver; duration band through510 minutes inclusive; SH06 actual
`now()-interval '12 months' <= StartDate < now()`. The latter remains a rolling
extraction window, not a claim that extraction is fixed-clock. Source generation
itself retains its previously verified fixed clock.

## All original production cells

The comparator consumes only the exact file's emitted Markdown and the unchanged
original production Markdown. It does not query replacements or consume generator
summaries. Missing/duplicate/unexpected buckets cannot pass. All profile headers,
row counts and **5,906 reference cells** are checked.

Both `ss_shape_v10` and `ss_shape_v10_rebuild` produced the same complete comparison,
with zero duplicate keys, missing buckets, indeterminate cells or unexpected buckets:

| Profile | Production / actual rows | Cells | Numeric within ±20% | Suppressed1..4 | Production withheld | Failed |
|---|---:|---:|---:|---:|---:|---:|
| SH01 |415 /415|3320|2914|406|0|0|
| SH02 |117 /117|234|184|25|25|0|
| SH03 |66 /66|198|171|9|18|0|
| SH04 |48 /48|96|94|1|1|0|
| SH05 |60 /60|240|228|3|9|0|
| SH06 |809 /809|1618|851|767|0|0|
| SH07 |25 /25|175|169|6|0|0|
| SH08 |25 /25|25|20|5|0|0|
| **Total** |**1565 /1565**|**5906**|**4631**|**1222**|**53**|**0**|

Denominators are explicit: **4,631 numeric**, **1,222 suppressed count bounds**,
**53 intentionally withheld metric values**. `<5` in emitted output establishes
1..4, not an invented exact count or a numeric ±20% pass. All53 withheld cells
remain in the inventory as **NotComparableProductionWithheld**, never passes:
SH02 share25; SH03 share9 +median9; SH04 share1; SH05 percentile9.
The complete bucket-by-bucket rationale (local-only: `2026-11-16-extractor-validity.md`)
retains all53. Neither original SQL nor further synthetic measurements can recover
these deliberately undisclosed production metrics.

The complete v10/rebuild comparison JSON files are byte-identical:
`C5478EABCD24FE68E53832D33E87D2959B3783E2733AFF40EAEA9D6979E9E6AC`.
Raw Markdown output hashes differ because SH00 correctly names each database and
its real extraction instant. This does not claim equal runtime metadata.

## Preserved v20 adverse source — deliberately not shape-fit

The source actually used by target v20 is `ss_adverse_valid_v18`, comment
`SS SYNTHETIC ADVERSE PAGING V18`, in `unified-ss-migration-source-1`.
It also executed the exact original successfully, read-only, without repair.
It is **not** the production-shaped source and is not relabelled:

| Comparison verdict | Reference cells |
|---|---:|
| MissingBucket |5437|
| Fail |230|
| FailSuppressedRange |3|
| ActualSuppressedIndeterminate |20|
| ActualWithheld |16|
| WithinRange |140|
| WithinSuppressedRange |7|
| NotComparableProductionWithheld |53|
| **Total** |**5906**|

Additionally31 unexpected buckets /171 emitted metric cells: SH01 18buckets,
SH05 1, SH06 11, SH08 1. Actual rows per profile:
18 /17 /17 /32 /20 /13 /25 /16 =158 rows. These aggregate response differences
are not lost-record counts. The verification wrapper correctly exits1 for this
fit failure while recording the actual **psql exit0** separately.

## Immutable private proof and hashes

All full rows, stdout, stderr, comparison and execution manifests remain ignored
under restricted `docker/migration/artifacts/original-sh-<label>/`.
Input and artifact parent ACL protection and Git ignore rules were verified.
No restricted input/output was staged.

| Database | Label | UTC execution interval |
|---|---|---|
| `ss_shape_v10` | `v10-20261117-v1` |14:42:25.591110–14:42:29.474209|
| `ss_shape_v10_rebuild` | `rebuild-20261117-v1` |14:42:25.575789–14:42:29.423926|
| `ss_adverse_valid_v18` | `adverse-v20-source-20261117-v1` |14:42:25.622361–14:42:27.783537|

| Database | stdout SHA256 | Execution SHA256 |
|---|---|---|
| v10 | `290112310CC4D187186DD2FC9ABA786EDA932DA7317BF733A15E6B9C7FCCAB01` | `8FFCE845D794B8B500EFDFED923099404B795A101E58117F13261C728AEC8887` |
| rebuild | `48F50F0DABAFF73ADCBA8ADC7E9E4E4FF2A1035E0660B099D693D039D0713FF8` | `8E3CBFC9602F6A35AABF6E635DB21C732B78402A8219DD21D4F42D6682FD50C7` |
| v20 source | `DE03DE99F054335AF3E1D1FDA5AE92869B7ABD02796D2787EF68554404FDE2D1` | `275612A7647D6E8C760E4F0D5D87741E2A1F4C637E38103CE3A96F25036BE481` |

All stderr hashes are the empty SHA256:
`E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`.
Execution SHA256 covers canonical JSON of exact argv/environment, input hashes,
guard, timestamps, status, output hashes and comparison result, excluding only the
execution-hash field itself. Full `execution.json` retains those values.

## Commands / remaining boundaries

From the repository, run with a **new label** (existing proof is never overwritten):

```text
rtk python docker/migration/verify_original_shape.py --database ss_shape_v10 --label <new-label>
rtk python docker/migration/verify_original_shape.py --database ss_shape_v10_rebuild --label <new-label>
rtk python docker/migration/verify_original_shape.py --database ss_adverse_valid_v18 --label <new-label>
```

Actual commands used the labels above; results0 /0 /1 respectively, with underlying
psql0 /0 /0. Python AST/compilation and independent all-cell/rebuild/manifest checks
apply to this tooling; no API/db/app workflow change requires a new SDK build.

Other EX/AG/AD/SP/OD/LX extractors were not executed and are not inferred from SH.
The previous exact original SH execution gap is now closed. Prior 22-table inventory,
source integrity, fixed-clock generation/rebuild and filled-DB refusal remain
separate retained evidence, not rerun or weakened here. Parent canonical refresh
and final mirror follow this result. Full SS acceptance remains NO-GO; no production
activation, fixture reset, attribution or push.
