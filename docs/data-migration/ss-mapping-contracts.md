# SS Mapping Contract Review Draft

**Status: Pending human approval. No approved mappings or enabled source tables.**

This is a repository-local review draft, not the canonical feature contract. The canonical
`specs/001-data-migration-jobs/contracts/ss-mapping-contracts.md` is outside this agent mode's
write scope. A feature owner must move the reviewed content there. T016 remains open.

## Evidence

- Workspace `documentation/data-migration/ss-erd.md` and `migration-job-design.md`.
- Read-only inspection of legacy SS `db/models/location/Region.cs`, `Location.cs`,
  `db/models/lookupcodes/LookupCode.cs`, `LookupSortOrder.cs`, and `db/models/abstract/BaseEntity.cs`.
- Legacy SS `db/migrations/SheriffDbContextModelSnapshot.cs` identifies the quoted physical
  tables and `timestamp with time zone` audit columns.

The ERD summary calls `Region.JustinId` a string. The model declares `int?`. This draft follows
the model only for source metadata preflight; the owner must confirm the deployed source type
and target conversion. No target identity rule is inferred from this field.

## Safety Boundary

- Both configured sources and the migration feature flag remain off by default.
- The SS job is registered with identity `data-migration:ss` but returns an empty cron while
  contracts are pending. The existing Hangfire helper removes any stale SS schedule.
- A disabled manual invocation fails without creating a run. An enabled manual invocation
  uses the existing durable SS run guard, records `MappingApprovalRequired`, finishes `Failed`,
  and throws a fixed safe error. No empty run is reported as `Completed`.
- No source connection is opened by the job. CASS is not registered, queried, or mutated.
- The separate SS preflight is callable only when the feature flag and SS source are enabled.
  It runs fixed named-column `LIMIT 0` queries in a read-only transaction and rolls it back.
  It returns checked table names only. It is not migration, reconciliation, or mapping approval.
- DTOs exclude `CreatedById`, `UpdatedById`, navigations, `xmin`, user credentials, and photos.
  No DTO serialisation, row-reading method, source payload logging, or target mapper exists.
- Empty, `disable`, and `disabled` cron values mean no schedule. Other enabled-source cron
  values must parse as five-field Cronos syntax; disabled sources are not validated.

## Reference Wave Candidates

All candidates are **Pending human approval**. Source identity candidates are
`(SS, physical table, invariant decimal Id)`; all four source keys are integers. Target keys
must not reuse source integers without an approved identity/conflict rule.

| Physical table | Explicit DTO/preflight columns | Dependency evidence | Unknown approval choices |
|---|---|---|---|
| Region | Id, JustinId, Code, Name, ExpiryDate, CreatedOn, UpdatedOn | No reference-table parent | Target match key; nullable JustinId conversion; code/name conflicts; expiry semantics |
| Location | Id, AgencyId, Name, JustinCode, ParentLocationId, RegionId, Timezone, ExpiryDate, CreatedOn, UpdatedOn | Optional RegionId; ParentLocationId has no FK in the documented model | AgencyId reconciliation; JustinCode target conversion; parent hierarchy; missing region; timezone validation; expiry semantics |
| LookupCode | Id, Type, Code, SubCode, Description, EffectiveDate, ExpiryDate, LocationId, Mandatory, ValidityPeriod, Category, AdvanceNotice, Rotating, CreatedOn, UpdatedOn | Optional LocationId | Integer enum translation; scope; target lookup identity; business meaning of scheduling/training fields; effective/expiry semantics |
| LookupSortOrder | Id, LookupCodeId, LocationId, SortOrder, CreatedOn, UpdatedOn | Required LookupCodeId; optional LocationId | Target code/location links; global versus local precedence; duplicate/conflicting sort order; delete semantics |

Preflight uses quoted tables in `public` as a candidate source schema. Confirm the actual
schema, column types/nullability, and role's read-only grants with the source owner. Type
preflight checks integer, boolean, text/varchar, and timestamptz compatibility. It does not
prove constraints, privileges, nullability, mapping correctness, or snapshot completeness.

## Incremental Contract Still Pending

- Proposed change time: `COALESCE(UpdatedOn, CreatedOn)`, in UTC, per physical table.
- Proposed ordering: change time then numeric `Id`. Do not compare integer IDs lexically.
- Approve replay-overlap duration, initial lower bound, and tied-timestamp payload changes.
- Distinguish a persisted completed watermark from an in-run paging cursor. Overlap applies
  at run start; paging must advance strictly within that replay window.
- Approve the hash field set and canonical representation, including nulls, enums, dates,
  strings, and expiry. Do not hash or retain excluded source data.
- Define deletion detection; an absent source row is not automatically a delete instruction.
- Define conflict priority for seeded target data and later user edits. Default is no overwrite.
- Define per-table rejection, rollback, watermark advancement, and reconciliation rules.
- Acceptance must account for every selected row as inserted, updated, unchanged, skipped,
  rejected, or failed, with zero duplicate source links and targets on unchanged replay.

No row queries, paging, hashes, watermark advancement, target writes, or mapping classes are
enabled by this draft. T019-T021 belong to the next PR after approval.

## People, Events, and Roster Waves

These contracts are **Pending human approval** and have no DTOs or queries in this PR.

| Candidate scope | Evidence and unresolved choices |
|---|---|
| People | Sheriff is TPH in User, not a physical Sheriff table. Approve exact discriminator values, system-user exclusion identity, user match/conflict rules, allowed fields, and target Sheriff identity. Do not project credentials or photos. |
| Events | SheriffActingRank, SheriffAwayLocation, SheriffLeave, and SheriffTraining need approved person/reference links, event type and time rules, expiry/deletion rules, and target identities. SheriffStandardTraining and Sheriff.Excused need explicit decisions; neither is silently omitted or mapped. |
| Roster | Assignment, Duty, DutySlot, and Shift need approved lookup/location/person links, unfilled-slot handling, nullable assignment links, timezones, conflict/deletion policy, and audit behaviour. |

## Test Coverage and Task State

No delegate tool is available in this session. The requested local follow-up explicitly
permits direct test authoring; tests follow nearby xUnit conventions and use the existing
SQLite context helper and hand-written source connection doubles.

Focused coverage adds 60 cases: 31 in `SsDataMigrationRecurringJobTests`, 28 in
`SsLegacyMigrationSourceTests`, and one SS-specific case in `RecurringJobHelperTests`.
All 64 cases in those three classes pass, including four existing helper cases. This is
new SS coverage, not a claim based only on the 12 foundation tests.

| Task | State in this slice |
|---|---|
| T014 | Complete locally: DI discovery/defaults, stable identity, stale SS-only schedule removal, feature/source gates, parsed valid/invalid cron, durable guard, explicit mapping failure, and CASS isolation are tested. |
| T015 | Partial: doubles cover exact zero-row projections, read-only transaction ordering, rollback/disposal, schema names/counts/types, SS-only connection selection, secret-safe errors, and cancellation. No watermark/replay row queries exist to test. Live PostgreSQL privileges/schema remain unverified. |
| T016 | Open: this draft is pending review and canonical placement. |
| T017 | Partial: disabled SS registration and durable failed-run boundary have focused coverage. Approved migration execution remains blocked. |
| T018 | Partial: reference DTOs and schema-only source preflight exist. Approved row queries and paging remain blocked. |
| T022 | Partial: SS adapter/job DI wiring exists; no mapper is registered. |
| T023 | Partial: SS-specific failed-run, guard, cancellation, storage-error redaction, and isolation paths are tested. Mapping-dependent idempotence, unchanged rows, and batch acceptance remain blocked. |

Job/preflight errors discard raw provider exception messages and inner exceptions.
Caller cancellation keeps its token but not provider text; unrelated provider cancellation
becomes a safe failure. Cancellation after guard acquisition does not interrupt the
mapping-failure evidence or failed-run completion writes.

Remaining risk: a storage failure after acquiring the guard may leave a durable `Running`
run that needs operator recovery. It is never reported as `Completed`. SQLite tests prove
the job's use of the unique guard but do not prove PostgreSQL cross-process concurrency.
Source doubles prove query choice and read-only command ordering, not deployed role grants.

Stop at the Phase 3 human gate. Do not enable either source, add business mappings, expose
operations endpoints, change GitOps, create a PR, or commit this slice.