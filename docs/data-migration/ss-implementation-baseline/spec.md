# Feature Specification: Independent Data Migration Jobs

**Feature Branch**: `Feature/SS-872-ss-migration-job`

**Created**: 2026-09-29

**Status**: Implementation authorized 2026-11-16; production enablement deferred

**Input**: Create independently configurable recurring migration jobs for Sheriff Scheduling (SS) and Court Admin Scheduling (CASS), based on the workspace migration-job design. The existing feature branch is configured for this work; no new branch is required.

## Clarifications

### Session 2026-11-18: Apply Opt-in

- SS apply requires an authenticated, audited API trigger with explicit `dryRun:false`
  and `AllowMigrationApply=true` for that job. Both defaults remain safe; configured
  job `DryRun=false` is not an additional apply requirement.
- Remove the rehearsal target-name restriction from apply only. Existing rollback
  stays rehearsal-only; production migration rollback is not available.
- A Report generation error is non-blocking and must not fail the loader runs. Record
  the Report as `NeedsAction` with safe diagnostics, not a pass. Real data failures
  remain failures and missing validation evidence remains visible.

### Session 2026-11-16

- Implement SS now using final findings; CASS and tenant deployment work stay deferred.
- Preserve user identity/authentication data where available, scheduling business meaning
  and training compliance data in Unified's workflows. These are the primary acceptance gates.
- Noncritical Stats classifications need documented logical assumptions, not perfect mappings.
- Split SS into MyTeam, Training and Scheduling loaders plus a final differences Report.
  Share SS identity crosswalks, not entity ownership; gate dependent runs by MyTeam lineage.
- Fix CutoverDate to `2026-12-01`; dates after it are Draft, dates on/before it Active.
  Retained leave types expire at cutover UTC midnight, superseding run-date expiry.
- Q-T6C client gate was cleared 2026-11-15; preserve SS training-category labels.
- Approved G-17/G-19 history differences remain findings, not data repairs or app changes.
- Build synthetic SS from source schema and aggregate extracts, not production row dumps.
  Keep the source DB running/persistent. Use an isolated target; do not reset shared local data.
- Migration-only changes in Unified. Move JC code-default and other app changes to
  [backlog](./backlog.md); require explicit false expiry flags in migration preflight.
- Commit logical milestones without attribution; do not push. No automatic production writes.

### Session 2026-09-29

- Q: Should this feature change tenant GitOps configuration, including AC1CC8? → A: Keep AC1CC8 unchanged. Roll out the required SS migration changes through CB6495; CASS tenant delivery is deferred.

### Session 2026-10-07

- Unified Scheduling is authoritative for Region, Location, and lookup data. SS reference migration links existing records and adds missing records only; it must not overwrite existing reference rows maintained by Justin synchronization or Unified users.
- All SS internal IDs resolve to Unified internal IDs through durable source-specific crosswalks. Legacy internal IDs must not be copied into Unified foreign keys.
- For non-reference records with approved mappings, legacy updates follow the Q-X4 fingerprint rule (2026-11-09): update a job-inserted target only when SS mapped values changed and the target's mapped values still match the fingerprint last written; otherwise preserve Unified data and log a conflict. Linked-only targets never update; the reference add-only rule takes precedence.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Run SS Migration Independently (Priority: P1)

An authorized operator enables and schedules the SS migration source without enabling or affecting CASS. The SS job reads only SS data and applies permitted changes to Unified.

**Why this priority**: SS and CASS must be delivered safely as separate migration paths. Each source needs a working, independently operable job before any shared migration outcome has value.

**Independent Test**: Configure only SS with a valid read-only source connection and a schedule. Verify that one SS run is recorded, CASS has no run, and only SS source records are processed.

**Acceptance Scenarios**:

1. **Given** an SS job is enabled with valid config and its lineage prerequisites hold while CASS is disabled, **When** its schedule is due, **Then** exactly one run of that job starts and no CASS run starts.
2. **Given** SS is disabled, **When** its prior schedule becomes due, **Then** no SS migration run starts.
3. **Given** SS and CASS have the same numeric source record ID, **When** both sources are migrated in separate approved runs, **Then** each source record remains independently identified and tracked.

---

### User Story 2 - Run CASS Migration Independently (Priority: P1)

An authorized operator enables and schedules the CASS migration source without enabling or affecting SS. The CASS job reads only CASS data and applies permitted changes to Unified.

**Why this priority**: CASS has different source structures and must not be coupled to the SS job, configuration, schedule, or control state.

**Independent Test**: Configure only CASS with a valid read-only source connection and a schedule. Verify that one CASS run is recorded, SS has no run, and only CASS source records are processed.

**Acceptance Scenarios**:

1. **Given** CASS is enabled with a valid connection and schedule and SS is disabled, **When** the CASS schedule is due, **Then** exactly one CASS migration run starts and no SS migration run starts.
2. **Given** CASS is disabled, **When** its prior schedule becomes due, **Then** no CASS migration run starts.
3. **Given** a CASS run is paused, aborted, or fails, **When** the next SS schedule is due, **Then** SS remains able to run according to its own configuration and controls.

---

### User Story 3 - Operate Safe, Repeatable Migration Runs (Priority: P2)

An authorized operator can review a source-specific run, pause or abort it safely, and retry it without duplicated target records or loss of audit evidence.

**Why this priority**: Initial migration and later incremental runs can affect justice-sector scheduling data. Operators need clear controls and a durable record of each outcome.

**Independent Test**: Trigger a source run with repeated and changed records, request an abort between batches, then retry. Verify that committed batches remain consistent, the active batch is not partially applied, and source-specific run, failure, and mutation evidence is available.

**Acceptance Scenarios**:

1. **Given** a source migration is running, **When** an operator requests an abort, **Then** the current batch fully completes or fully rolls back and the run records an aborted outcome.
2. **Given** a source record is unchanged since a prior successful run, **When** that source is re-run, **Then** no duplicate target record is created.
3. **Given** a row cannot be migrated, **When** its batch is processed, **Then** the failed batch is rolled back, redacted failure evidence is retained, independent rows can continue and the row remains retryable despite watermark progress.
4. **Given** Training and Scheduling completed from one MyTeam lineage, **When** Report runs,
   **Then** it accounts for every selected source row and never returns Pass while a primary-goal
   failure, unexplained difference or NeedsAction remains.
5. **Given** later user edits or dependent work exist, **When** rollback is requested,
   **Then** preview/export occurs first and no business writes occur without explicit,
   current-impact confirmation for the selected run.
6. **Given** an SS apply is requested, **When** the request is not an audited API trigger
   or the selected job has not opted in, **Then** no apply starts.
7. **Given** an acquired Report cannot be generated and run state is writable,
   **When** its run ends, **Then** it records `NeedsAction` without changing loader
   outcomes or claiming a pass.

### Edge Cases

- A source schedule is empty or marked disabled: its recurring job is removed or does not run.
- A source is enabled without a valid connection or schedule: deployment or startup validation fails before that source can run.
- A job is manually triggered, restarted, or its scheduler lock expires: a durable
  `(Source, Job)` guard prevents overlapping execution of that job. Atomic lineage gates
  allow Training/Scheduling parallelism but prevent parent/dependent overlap.
- Many source rows share the same change time: ordered composite watermarks and replay-safe processing prevent skipped or duplicated records.
- A later Unified edit blocks ordinary migration update. Rollback instead requires a
  restricted impact export and explicit data-loss confirmation before it may revert that edit.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: SS MUST provide MyTeam, Training, Scheduling and final Report recurring jobs.
  CASS remains a separate disabled source with deferred implementation.
- **FR-002**: Each source MUST have an independent connection. Each job MUST have
  independent enablement, schedule, controls, run state, watermarks, failures, and
  rollback evidence; SS jobs share their source connection.
- **FR-003**: Disabling one source MUST prevent its future scheduled runs without changing the configuration or execution of the other source.
- **FR-004**: Both sources MUST be disabled by default. A source MUST validate its connection, schedule, and batch settings only when that source is enabled.
- **FR-005**: Each source MUST use its own read-only legacy credentials. Credentials and source payloads MUST NOT be logged or stored in migration evidence.
- **FR-006**: A source job MUST use Pacific time for its configured schedule and treat an empty or disabled schedule as no active recurring job.
- **FR-007**: The system MUST allow only one active run per `(Source, Job)`, with atomic
  dependency/lineage checks and safe stale-run recovery that cannot permit an old worker to write.
- **FR-008**: Each source record MUST be identified by its source, entity type, and legacy identifier. SS and CASS records with equal numeric identifiers MUST remain distinct.
- **FR-009**: Re-running a source job MUST be safe: records that have not changed since their last successful application MUST NOT create duplicate targets or duplicate migration links.
- **FR-010**: Each migration batch MUST apply all target changes and migration evidence together, or apply none of them. A failed batch MUST produce separate, redacted failure evidence after rollback.
- **FR-011**: The system MUST retain source-specific run outcomes, record links, failures, mutations, watermarks, and operational controls so an operator can reconcile and recover a run.
- **FR-012**: An authorized operator MUST be able to pause, abort, and request a compensating rollback for one source without changing the other source's data or controls.
- **FR-013**: Rollback MUST follow dependent-before-parent order and require a restricted,
  immutable impact export plus explicit run-bound data-loss confirmation before reverting
  later edits/dependent work. It MUST preserve unrelated work and link-only targets,
  commit atomically per job, reset owned progress and verify migration-footprint hashes.
- **FR-014**: New source tables and mappings MUST remain source-specific. Removing the SS migration path MUST NOT affect the CASS path, and vice versa.
- **FR-015**: Each included legacy table MUST have an approved mapping contract that defines its scope, source and target identity, field and timestamp rules, delete and conflict handling, dependencies, and acceptance checks before its data is enabled.
- **FR-016**: This implementation MUST leave both tenant repositories unchanged. CB6495
  source secrets/network/enablement remain tracked deployment follow-up, not a local-build gate.
- **FR-017**: SS Region, Location, and lookup migration MUST treat Unified as authoritative, link existing targets without mutation, and insert missing targets only under approved semantic identity and dependency rules. Legacy internal IDs MUST resolve through durable source-specific links to Unified target IDs.
- **FR-018**: For approved SS non-reference mappings, updates to existing job-inserted targets MUST require (a) mapped SS values whose fingerprint differs from the stored `PayloadHash` and (b) current target mapped values whose fingerprint equals the stored `PayloadHash`, rechecked in the write transaction. Otherwise the target MUST NOT be written; a conflict MUST be logged and counted as skipped. Source timestamps select rows only; Unified `UpdatedOn` MUST NOT be used for decisions (amended 2026-11-09, Q-X4).
- **FR-019**: SS history starts at configurable `2020-01-01` Vancouver midnight with
  inclusive EndDate boundary regardless of row timezone. Invalid config blocks writes.
  Earlier changes trigger resumable, idempotent backfill independent of normal watermarks,
  with Q-X4 protection. Later changes delete nothing (amended 2026-11-17: 2015 -> 2020). Include unexpired Assignments and
  every Assignment referenced by an included Duty; people/roles/needed references are unfiltered.
- **FR-020**: All SS jobs MUST use fixed required CutoverDate `2026-12-01`, lock it after
  applied work and preserve status/series/leave-type expiry on replay.
- **FR-021**: MyTeam owns identities/references/people events; Training owns courses,
  histories and qualifying profile assignments; Scheduling owns roster structures.
  No mapped target field/entity may have competing job writers.
- **FR-022**: Training and Scheduling MUST reference the latest Completed MyTeam run and
  may run in parallel. Report MUST use completed same-lineage results with none Running.
- **FR-023**: Every selected source row MUST have one final outcome. Missing coverage,
  unknown/rejected/failed findings, threshold breaches and unresolved primary-goal failures
  MUST NOT yield Pass. Open NeedsAction MUST NOT yield PassWithKnownDifferences.
- **FR-024**: Dry runs MUST use the same mappers and real DML rolled back, persist only
  migration evidence/staging, and never expose staged identities as applied crosswalks.
  Dependent FK simulation must use real staged parents or report MissingParent explicitly.
- **FR-025**: Migration MUST preserve authentication identifiers where possible, exact
  schedule business intervals/relationships and eligible training dates/expiry/notes/versions.
  It MUST NOT invent attendance, recalculate migrated certificate expiry, send notices or
  invoke user-creation mandatory-training side effects.
- **FR-026**: Validation MUST use persistent Docker synthetic SS data from the actual SS
  schema and aggregate production shape, plus actual SS/Unified report/service comparisons.
  No production row dump, false pass, removed adverse case or target-app workaround is allowed.
- **FR-027**: SS preflight MUST require all three JC geography expiry flags explicitly false.
  Non-migration app changes MUST instead be recorded as recommendations in the 001 backlog.
- **FR-028**: Findings/CSV/Markdown MUST expose significant omissions, accepted differences,
  safe source IDs/metrics and human actions. Ordinary evidence MUST contain no protected free
  text/photos/credentials; rollback impact values require separate restricted export controls.
- **FR-029**: SS apply MUST require an authenticated, audited API trigger with explicit
  `dryRun:false` and that job's `AllowMigrationApply=true`. Both request and job defaults
  MUST deny apply. Scheduled and direct execution MUST NOT apply.
- **FR-030**: A Report generation error MUST NOT change loader outcomes or mark the Report
  migration run as failed or passed, or increment its failure count. When run state is
  writable it MUST remain visible as `NeedsAction` with safe diagnostics; persistence
  errors MUST be surfaced. Genuine migration and data-validation failures MUST remain failures.

### Key Entities *(include if feature involves data)*

- **Migration Source**: One of SS or CASS, with separate enablement, connection, schedule, and operational controls.
- **Migration Run**: A source-specific execution with lifecycle state, time bounds, watermarks, mode, counts, outcome, and optional rollback link.
- **Migration Record**: The durable link between a legacy source record and its Unified target, used for idempotence and incremental processing.
- **Migration Failure**: Redacted evidence of a source row or batch that could not be migrated.
- **Migration Mutation**: A source run's ordered proof of a target change, used for safe compensating rollback.
- **Migration Control**: The current pause or abort state for one source.
- **Mapping Contract**: The approved source-table rule set for migration scope, identity, transformation, dependencies, conflicts, and validation.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In an environment where only one source is enabled, 100% of scheduled runs start only for that enabled source; the disabled source has zero runs.
- **SC-002**: For each source, 100% of records re-read without a source change result in zero duplicate target records and zero duplicate source-to-target links.
- **SC-003**: During a forced batch failure, 100% of target changes from that batch are absent after rollback, and one redacted failure outcome is available for operator review.
- **SC-004**: At every observed time, each `(Source, Job)` has at most one active run and
  all observed parent/dependent overlaps obey FR-022.
- **SC-005**: Before enabling a source table, its mapping contract and reconciliation report account for 100% of selected source records as inserted, updated, unchanged, skipped, rejected, or failed.
- **SC-006**: A source abort request stops further batch selection within one batch boundary and leaves the other source's scheduling and controls unchanged.
- **SC-007**: First load, unchanged replay, changed-source replay, later-target edit,
  batch failure, interruption/resume and earlier-cutoff backfill preserve required business
  data without duplicate targets or links.
- **SC-008**: Comparable unsuppressed synthetic SH volume/distribution buckets fit production
  within +/-20%; suppressed `<5` buckets use ranges. Unmet shape buckets remain explicit failures.
- **SC-009**: O-1..O-4 actual report/service comparisons account for every difference through
  a known finding in past, cutover and future windows; no unexplained training/schedule loss.
  O-5 overtime parity is deferred to BL-17 and is not required for this release.
- **SC-010**: Persistent synthetic source remains healthy after work; isolated target can
  be recreated without touching shared application data.

## Assumptions

- `Feature/SS-872-ss-migration-job` is already configured. No branch creation is needed.
- All jobs begin disabled/dry-run in application configuration. Local isolated rehearsal is
  in scope; CB6495 rollout and CASS delivery remain separate.
- The source-to-target mapping contracts in the migration design remain the approval gate for adding legacy tables. This feature does not silently infer unsupported mappings.
- Source credentials are supplied outside source control and are read-only. Target migration writes use the existing approved application data path or an explicitly provisioned migration writer.
- Production rollout is deferred until dry-run reconciliation and security approvals are complete.
- The recurring job service and its existing operational dashboard are supplementary observability tools; durable migration records are the system of record for migration truth.

## Scope Boundaries

- This build includes SS migration-only implementation, migration state, synthetic Docker
  source/isolated target, report oracle/comparison harnesses, recovery, docs and logical commits.
- Excludes production access/writes, pushes, tenant changes, CASS implementation, ordinary
  Unified workflow changes and legacy application behavior changes. Synthetic oracle-only
  test/harness work may use legacy code without changing its business behavior.