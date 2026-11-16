# Migration State Model

**Design baseline 2026-11-16; not a claim that schema is implemented.**
Only migration-owned persistence may change. Existing scheduling/training/auth models remain.

| Entity | Required state / constraints |
|---|---|
| DataMigrationRun | ID, Source, Job, status, dry-run mode, immutable cutover/history dates, UTC start/end, prerequisite MyTeam ID, restore-point ID, heartbeat/lease/fencing identity, counters, verdict, rollback lineage |
| DataMigrationRecord | Live `(Source,EntityType,LegacyId)` unique; actual TargetType/Key; source timestamp/fingerprint and mapped target PayloadHash/AfterHash; inserted-vs-linked ownership; last applied run; deletion bookkeeping never authorizes target delete |
| DataMigrationFailure | Run/job/source/entity/key, safe class/reason and timestamp; no raw exception/payload/credentials |
| DataMigrationMutation | Run, sequence, target/record identity, create/update operation, protected minimal before-image, after hash; merged-target ownership cannot depend on just one member |
| DataMigrationControl | `(Source,Job)` unique; source-wide control supported, pause/abort plus changed actor/time |
| DataMigrationWatermark | `(Source,physical entity)` unique; changed UTC + typed source-ID cursor, upper bound and last run; atomic with applied batch |
| Backfill progress | Source/entity, prior/applied/requested cutoff, independent cursor/range/status; interrupted earlier-cutoff expansion resumes without normal-watermark loss |
| Retry state | Source/entity/key, blocked/rejected class, dependency and attempts; selected independently of watermark until resolved or explicitly excluded |
| Dry-run candidate | Separate lineage-scoped source/target candidate identities; never a live crosswalk; enough staged parent shape for real rolled-back dependent DML or explicit MissingParent |
| DataMigrationFinding | Report/source run, Job, entity/key or group key, optional target, outcome, class/rule/severity, source/target metrics, safe detail; index `(RunId,Outcome,ClassCode)` |
| Rollback impact | Run + immutable impact digest, restricted export reference, confirmation state, touched-row footprint before/after; PII-bearing values separate from ordinary findings |

These are responsibilities, not a mandate for one new table per row: reuse existing migration
state where it preserves clear constraints. Never overload source business tables for staging.

## Concurrency And Lineage

- Partial unique Running index on `(Source,Job)`, not Source alone.
- Advisory transaction lock serializes gate check/run creation for SS; Training/Scheduling
  may then run concurrently after the same latest Completed MyTeam.
- Parent start/rollback cannot race applied/running dependants; Report waits for same-lineage
  completion. Failed/stale dependencies are not completion.
- An expired lease cannot authorize a stale worker to commit after takeover.
- Every mapped target field has one job owner; Training owns migrated profile assignment.

## State Transitions

Run: Queued -> Running -> Completed / Failed / Aborted; successful confirmed compensation
-> RolledBack. Report verdict is separate: Pass / PassWithKnownDifferences / NeedsAction /
Fail. Completed loader does not imply accepted migration.

Batch: select bounded source set -> validate -> transaction -> targets + links + mutation +
counts + progress -> commit. Failure rolls back all batch state; safe failure/retry evidence
persists separately. Dry-run business transaction always rolls back.

## Hash Rules

Canonical explicit mapped fields, stable property order, invariant typed/null encoding,
UTC round-trip instants, enum names, exact mapped text and resolved IDs. Exclude source
audits, ignored fields and photos. Distinguish source fingerprint from target AfterHash
where necessary. Group hash includes sorted complete source membership. Order, versions,
cutover statuses and insert-only defaults remain stable on replay.

## Findings Coverage

Each selected source row has one outcome; group metrics such as overtime are separately
tagged so coverage is not inflated. Many-to-one merges still have one live crosswalk per
member. Synthetic category/series keys are deterministic. Approved exclusions have no fake
target/link. Dry-run candidate identities never satisfy an applied dependency.

## Data Protection

Migration hashes are not payload storage. Findings contain source IDs/counts/metrics only.
Before-images/rollback exports may contain business values and require least privilege,
restricted retention and explicit confirmation; photos/credentials never enter them.
