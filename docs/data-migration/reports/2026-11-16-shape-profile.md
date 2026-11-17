# T060: actual deterministic SH01–08 fitted source

**Local independent SQL fit/proof passed. Exact original SH execution now also
verified on2026-11-17; full migration/business acceptance remains incomplete.
Production disabled.** [Original-file execution/hashes and all5,906 cells](2026-11-17-original-sh-execution.md).

## Retained source and provenance

- Separate persistent PG12.22 service `unified-ss-migration-shape-source-1`,
  named volume `unified-ss-migration_source-shape-v7`, no host ports.
- New, independently generated/loaded **`ss_shape_v10` / `ss_shape_v10_rebuild`**.
  Both marked `SS SYNTHETIC SHAPE V10`, default transaction read-only.
  All22 public tables grant reader SELECT; no reader table DML privilege.
- Fixed clock **2026-11-16T05:59:21Z**, seed `ss-shape-v9-20261116` retained across
  the final generator-only parent-placement correction. Not wall-clock generation.
- Actual supplied EF SS schema, not a reduced imitation. Input schema SHA256
  `DD683E36A7E3F11025DEEF2F51D2BF3AEEC3555DA06FA2DFD6D7F209CAE7384C`.
- Aggregate contract SHA256
  `C06BD03770951358B619A7E2AF36C0DAE70A9BAA111497AE134DF75C8FCA0F40`.
- Source PG12 Vancouver timezone file SHA256
  `B249CA1F48D23D66A6F831DF337E6A5ECF0D6A6EDDE5316591423D4A0C6BCB28`.
  The generator uses that actual server file, not newer host2026 rules. The isolated
  Compose image is digest-pinned; no target app timezone behavior was modified.
- Python3.14.7 / NumPy2.5.5 / SciPy1.18.1. Integer transport/workload allocation,
  deterministic invented identities/text, actual FK rows and actual pattern joins.
  Distribution fitting invents unconstrained tails/correlations; it does not recover
  any original individual, work assignment or production free text.

## Complete local extractor inventory and fit

Independent SQL reads **actual stored rows**, never generator summary counts. Every
reference cell is enumerated and unexpected actual buckets fail. Numeric ranges remain
the original inclusive ±20%; zero stays zero. `<5` means1..4. Suppressed null
share/quantile cells are explicitly **NotComparable**, never acceptance passes.

| Profile | Reference rows | Cells | Non-suppressed within ±20% | Suppressed bounds1..4 | NotComparable | Failed |
|---|---:|---:|---:|---:|---:|---:|
| SH01 location/year |415|3320|2914|406|0|0|
| SH02 local start hour |117|234|184|25|25|0|
| SH03 duration |66|198|171|9|18|0|
| SH04 weekday |48|96|94|1|1|0|
| SH05 user workload |60|240|228|3|9|0|
| SH06 assignment patterns |809|1618|851|767|0|0|
| SH07 field presence/length |25|175|169|6|0|0|
| SH08 census/roles |25|25|20|5|0|0|
| **Total SH01–08** |**1565**|**5906**|**4631**|**1222**|**53**|**0**|

SH00 additionally has5 metadata input rows, not5 business acceptance assertions.
Both databases were independently measured with the same complete zero-failure table.
Full per-cell references, actual values, exact ranges and verdicts are retained:
`docker/migration/artifacts/shape-v10/ss_shape_v10-fit.json` and
`ss_shape_v10_rebuild-fit.json`. These ignored artifacts contain comparisons, not
reconstructed target-service results.

**Historical local-only boundary:** the original local fit JSON retains
`original_external_extractors_executed=false`; it is not retroactively rewritten.
On2026-11-17, unchanged workspace SH SQL/Markdown were copied directly and the exact
file executed successfully on both retained shape databases and the v20 adverse
source. Original510-minute edge and rolling last12months bounds are verified without
substitution. New execution manifests independently confirm this complete fit table.
Other EX/AG/AD/SP/OD/LX were **not executed**, not inferred from SH. Do not equate
this shape gate with complete migration acceptance.

## Exact actual public inventory (including schema seeds)

| Table | Columns | Actual rows |
|---|---:|---:|
| Assignment |23|2767|
| Audit |7|0|
| DataProtectionKeys |3|0|
| Duty |12|101904|
| DutySlot |16|75164|
| JcSynchronization |2|0|
| Location |12|79|
| LookupCode |17|97|
| LookupSortOrder |8|7|
| Permission |7|37|
| Region |9|3|
| Role |7|6|
| RolePermission |7|55|
| SheriffActingRank |13|1165|
| SheriffAwayLocation |13|7397|
| SheriffLeave |13|12446|
| SheriffStandardTraining |15|0|
| SheriffTraining |16|21099|
| Shift |14|115433|
| User |21|873|
| UserRole |10|316|
| __EFMigrationsHistory |2|88|

Users =872 Sheriffs (656 enabled/216 disabled) +1 system user. The old80-sheriff
fixture is **not** relabeled production-fit. Seven workload tables contain334608
actual rows, including2015 training and2027 scheduling. Assignment total2767 =
1629 live pattern members +1138 expired; all809 measured pattern keys use actual
joined duty memberships. Role/user distributions and optional/text-length profiles
are measured, not just row totals.

Actual PostgreSQL checks: **zero** unvalidated FKs, live slots outside their parent
interval, slot/parent location mismatches and Duty/Assignment location mismatches.
Ordinary FK constraints were active throughout load; no constraint disabling.

## Independent rebuild and refusal proof

`generate` ran again from the same clock/seed, followed by a fresh second schema's
atomic load and independent SQL measurement. It was not a database clone or a second
copy of the first generated file.

Actual canonical table/sequence digest for **both** independently generated databases:

`C3A0FAB16256FAA6E5EB8D22ABB06AF54B12CC25ED1F941633FD7FC8931270E0`

Includes22 public tables /20 public sequences; audit/keyring/JC/permission payloads
are COUNT-only. **Photo bytes and source Region.Code are excluded**, not hashed.
Invented photos exist only in source User.Photo, never serialized generated JSON.
Photo-presence/length/stamp metadata is not a byte hash.

Actual repeat loads of both filled databases failed with the specific pre-mutation
`Refused: filled shape database` guard and unchanged canonical digest. Additional
primary refusal checks physical business `ctid/xmin` receipts so photo-only or
Region.Code-only updates cannot hide behind deliberately omitted value hashes.
No filled source was repaired/reset. Scripts/commands:
[reproduction instructions](../../../docker/migration/SHAPE-PROFILE.md).

## Retained failures and adverse boundary

- Original80-sheriff SH08 **FAIL24/26** remains historical evidence and unchanged source.
- Retained `ss_shape_v7`:24 failed cells and148 unresolved slot-parent placements.
  Host/source future timezone-rule mismatch explained the hour/weekday failures.
- Retained v8/v9/v9_rebuild: all SH cells fit but one zero-duration midnight slot
  remained outside its parent. They were not silently repaired or accepted.
- Final hybrid whole-schedule/hour-duration exchange preserves measured marginals,
  each location/year and fixed last12months membership; used parent rows never move.
  Unresolved containment now aborts generation **before load**. Two intermediate
  v10 attempts rejected17/23 unresolved placements before writing either fresh DB.
- [Actual full-adverse chain attempt](2026-11-16-adverse-v7.md): MyTeam Failed48 rejects;
  exact2233-row identity/outcome accounting;11 exact planted case outcomes;
  **3377 dependent cases unexecuted**, normal lineage gate intact. No escaped target
  business rows/audits/sequences or applied crosswalks. Not full adverse acceptance.

## Validation / remaining work

SDK10 Runner build **PASS** (6 existing NU1510 warnings), focused tests
**76 pass/0fail/0skip**, CSharpier check **PASS**, Python compilation **PASS**,
PowerShell parse **PASS**, final Compose validation **PASS**. Real PostgreSQL source
fit/rebuild/grant/integrity/refusal and adverse safety/accounting checks passed.
These operational checks are not additional independently maintained xUnit coverage.
Required `rtk git diff --check` returned2 on the existing CRLF Markdown convention;
the explicit-path staged check with command-local `core.whitespace=cr-at-eol`
passed. No global/repo Git whitespace setting was changed.

Subsequent evidence now includes exact original SH execution and full valid-parent
adverse execution; their separate reports retain the failures and limits. Other
extractor families, broader independent coverage and final parent canonical/mirror
refresh remain. No bc-design-agent or deployment change needed.
**BL15 future scheduling remains a measured primary blocker** (2 presence +4 conflict
response differences); no app behavior fix, migration workaround or loss waiver.
No publish/push, no production activation, no private artifacts staged.
