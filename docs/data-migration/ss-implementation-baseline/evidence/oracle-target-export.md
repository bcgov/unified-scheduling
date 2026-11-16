# Target export handoff — v1

For the migration owner/parent: export **actual target service/report output** independently,
then adapt identities/representation only to the source artifact contract below. Do not
reimplement SS selection/status/OT rules in SQL or copy source rows to manufacture parity.
If the target cannot expose a surface, report that gap rather than generate matching rows.
No target parity has been established by the source harness.

## Envelope

Use the metadata from a source `source-all-v1-*.json` artifact:
`Format`, `Marker`, `Scope`, `HistoryStart`, `Cutover`, `PermissionEvaluation`,
`OvertimeHoursPerDay`, and all three `Windows` must match exactly.
`SourceRevision`, `FixtureCounts`, `DatabaseSafety` are source provenance, not target results.
`Rows` is a flat array of `{ "Window": "...", "Report": "...", "Key": "...", "Value": "..." }`.
**Key and Value are JSON-encoded strings**, not nested objects. Each string contains the
object fields listed below. Parent can compare a normalized target `Scope=all` snapshot
with source `compare`, or a training-only snapshot with `compare-training`.

IDs must map back to original **source** identities: sheriff GUID; training course,
location, region, shift, duty, duty-slot and assignment integer IDs. Preserve nulls.
Never infer course identity from description: the stable fixture contains duplicate active
descriptions. Source identities are recovered through actual per-course TrainingService
calls, whose combined DTO multiset is checked against the unfiltered service report.
The raw training-API import command still rejects ambiguous descriptions; this normalized
ID-bearing contract is required for the owner's target export.

## Row objects

| Report | Key fields | Value fields |
|---|---|---|
| `training-api` | `sheriffId`, `courseId` | `locationId`, `regionId`, `end`, `expiry`, `excused`, `status`, `rowVariant` |
| `training-ui-projection` | `sheriffId`, `courseId` | `locationId`, `regionId`, `end`, `expiry`, `status` |
| `shifts` | `locationId`, `shiftId` | `sheriffId`, `start`, `end`, `timezone`, `anticipatedAssignmentId`, `workSection`, `overtime`, `slotIds` |
| `duties` | `locationId`, `dutyId` | `assignmentId`, `start`, `end`, `timezone`, `slotIds` |
| `duty-slots` | `locationId`, `dutyId`, `slotId` | `sheriffId`, `start`, `end`, `timezone`, `notRequired`, `notAvailable`, `overtime`, `closed` |
| `distribution-sheriffs` | `locationId`, `sheriffId` | `start`, `end` |
| `distribution-conflicts` | `locationId`, `sheriffId`, `kind`, `shiftId`, `start`, `end` | `conflictLocationId`, `timezone`, `workSection`, `overtime`, `eventType`, `slotIds` |
| `overtime-calculated` | `locationId`, `sheriffId`, `localDay`, `timezone` | `hours` |
| `overtime-allocation` | `locationId`, `shiftId` | `hours` |

Dates: UTC round-trip `O` strings (seven fractional digits, `+00:00`), or null;
`localDay`: `yyyy-MM-dd` in the key's IANA timezone. Slot ID arrays are ascending;
`slotIds=null` differs from `[]`. Booleans remain booleans. OT values are numeric hours.
`kind` is the actual enum label (`Training`, `Leave`, `AwayLocation`, `Scheduled`).
Property and row order do not matter; duplicate row multiplicity **does** matter.
No display names, email, notes or comments belong in normalized exports.

Keep actual API status strings, including empty string. Missing mandatory training is
`Not Taken` / `danger`; nonrotating grace-year status is `Requalification` / `warning`;
do not migrate these into “expired” to match a new target model. A mismatch is evidence.
The source `training-ui-projection` is **not executed UI/CSV evidence**: it only excludes
Excused and beautifies labels. Treat it separately from actual rendered target exports.

Fixed windows are documented in README and recorded in the source artifact. Source daily
OT is calculated across locations; the row key's location identifies the calling report
context, not an instruction to sum/recompute only that location's shifts.

## Owner execution inputs

`Invoke-Oracle.ps1` accepts `-EnvFile <restricted local env-file>` and
`-SourceContainer unified-ss-migration-source-1`. Docker consumes the env-file directly;
the wrapper never reads/prints its values. It must contain `SS_SYNTHETIC_DB` (explicit DB,
loopback host/5432 and synthetic account) and `SS_SYNTHETIC_ACK=SS SYNTHETIC`.
The wrapper sets `SS_ORACLE_SOURCE_REVISION` from git and uses
`--network container:<SourceContainer>`; no published port is needed.
Only the mock owner starts/seeds/mutates the source.
