# Tasks: SS Migration And Validity

**Updated 2026-11-16.** [Plan](./plan.md), [spec](./spec.md),
[canonical contract](./contracts/ss-mapping-contracts.md), [backlog](./backlog.md).
Existing IDs retained. Checked foundation tasks are inherited history, not new test results.
Implementation is authorized; production writes/pushes/non-migration app changes are not.

**Current disposition:** migration implementation and focused runtime proofs are delivered;
acceptance remains NO-GO. Exact original SH SQL passes two shape mocks. BL-15 and unresolved
source/adverse/report/OT differences remain explicit. Unchecked compound tasks retain broader
verification or acceptance work; see the [measured acceptance inventory](../../unified-scheduling/docs/data-migration/reports/2026-11-16-acceptance-inventory.md).

## Phase 0: Planning Milestone

- [x] T016 Reconcile final Q/G decisions into canonical SS contract; distinguish build authorization from production-write approval.
- [x] T053 Update spec, plan, data model, operations contract, quickstart and research for three loaders plus final Report.
- [x] T054 Transfer BL-1..6 and all named app gaps to the 001 backlog; keep app fixes out of implementation.
- [x] T055 Persist repo-local implementation spec/runbook and workspace artifacts in a logical migration-only commit; preserve pre-existing deleted draft. Initial mirror: `b589bee4`; refresh final evidence before delivery.

## Existing Foundation (Inherited)

- [x] T001 Add Unified.DataMigration project and references.
- [x] T002 Add module entry point/folders.
- [x] T003 Disabled SS/CASS config defaults.
- [x] T004 Local source environment scaffolding.
- [x] T005 API module registration.
- [x] T006 Feature flags and conditional options validation.
- [x] T007 Migration run/record/failure/mutation/control/watermark models.
- [x] T008 Model constraints/indexes.
- [x] T009 Foundation EF migration/context registration.
- [x] T010 Source-neutral and source-specific contracts.
- [x] T011 Composite watermark/hash/transaction primitives.
- [x] T012 Durable active-run guard.
- [x] T013 Foundation tests.
- [x] T014 Original SS registration/isolation tests (expand for split under T056).
- [x] T052 UpdatedOn follow-up handled by a separate workflow; no changes here.

## Phase 1: Job/State And Persistent Mock

- [x] T056 Replace blocked single SS shell with MyTeam, Training, Scheduling and Report jobs; per-job enabled/cron/dry-run/batch options and ownership validation. Remove old recurring identity safely; CASS untouched. Actual ready/adverse jobs and scoped operations executed; acceptance remains separate.
- [ ] T057 Add Job/lineage/cutover/restore-point/heartbeat or fenced-lease state, `(Source,Job)` Running guard, per-job controls and source-wide controls in migration-owned schema. Atomic dependency gate permits parallel Training/Scheduling, not parent overlap; stale-run recovery cannot allow old-worker writes.
- [ ] T058 Implement typed paging upper bounds, deterministic mapped/source hashes, full merge-group reloads, durable rejected-row retry and separate resumable cutoff-backfill progress (EN-1/4/5).
- [x] T059 Inspect/start/preserve existing stopped SS mock; add isolated persistent Compose resources, named volumes, synthetic seed/schema provenance and health checks. Healthy source/target retained; original source preserved. See measured report; shape fit remains T060.
- [x] T060 Tune deterministic synthetic data from production SH-01..08; measure ±20% fit on non-suppressed comparable buckets; verify repeated-build hashes, filled-DB refusal, all extractors and manifest cases (SS-V056). Exact original SH SQL in `d3a05fe6` passes both independent shape builds: 4,631 numeric cells within bounds, 1,222 suppressed bounds satisfied, 53 production-withheld cells explicitly noncomparable; all 5,906 cells accounted. Adverse fixture is separate and correctly fails shape fit; manifest outcome mismatches remain T048, not waived.
- [ ] T015 Extend adapter watermark/tied-time/read-only/cutoff and backfill tests (SS-V026).
- [x] T017 Retire original MappingApprovalRequired shell; implementation now belongs to T056.
- [x] T018 Implement all required read-only source projections/schema/timezone preflight; verify actual singular SS table/column names. Actual source reads account for 101,426 selected identities.
- [ ] T051 Migration-only JC guard requires all three expiry flags explicitly false, with no business writes on failure. Old JC code-default change moved to BL-9. Validate SS-V036/038/039/048 without altering JC behavior.

## Phase 2: MyTeam

- [ ] T019 Implement reference/type mappings under final contract; per-job ownership splits Type 6/0 into later jobs. Existing references unchanged, generated IDs/crosswalks, safe append locking with ordinary writers.
- [ ] T020 Implement users/roles/UserRole/acting/leave/away with identity/auth preservation, null Gender Other, exact dates/text, no UserCreatedSignal or invented training. Training alone owns profile assignment.
- [ ] T022 Wire source/mappers/jobs and disabled-by-default configuration without CASS coupling.
- [ ] T023 Add MyTeam PostgreSQL integration cases for idempotence, links, later-user edits, row rejection/retry and batch atomicity.

## Phase 3: Training And Scheduling

- [ ] T061 Implement categories/courses/profile requirements/history/versioning/qualifying profiles. Preserve certificate expiry including null; exclusions/notes/notice exact; no renewal/mail side effects (EN-6).
- [ ] T021 Implement classifications, merged definitions/shifts, duties/slots/links and qualifying G-10 series in required order. No invented shift gap time/occurrences.
- [ ] T062 Validate capacities, many-to-one identities, source membership changes, future Draft publish readiness, cutover-day series compatibility and retained expired leave editing (SS-V014..022/050..052).
- [ ] T063 Verify overtime using actual WorkingHoursService and configured 420/60/0; report every discrepancy and threshold. Future difference is not silently accepted.

## Phase 4: Reports, Operations And Recovery

- [ ] T034 Add pause/resume/abort/rollback/dry-run tests for split lineage and races.
- [x] T035 Implement durable source/job controls and safe status/failure queries. Actual PostgreSQL operations proofs pass.
- [x] T036 Implement preview-first confirmed compensating rollback, dependency order, merge ownership, footprint proof and transactional failure handling (SS-V055). Graph inverse restores prior footprint; broader release coverage remains T048/T050.
- [x] T037 Add job-scoped operations contracts/DTOs.
- [x] T038 Add existing-framework migration permission checks/endpoints; no auth framework changes.
- [x] T039 Verify authorization, source/job isolation, redaction and stable run IDs. Focused operations tests and actual PostgreSQL proofs pass; exhaustive release review remains separate.
- [x] T040 Safe structured logging only. Operations findings/exports are redacted; protected rows remain restricted.
- [x] T064 Implement findings/outcome coverage catalogue, fail-closed verdicts, same-lineage final Report and CSV/Markdown exports (SS-V053). Open NeedsAction never passes. Actual ready Report fails differences; invalid adverse lineage rejects Report acquisition.
- [x] T065 Validate mode runs real DML then rollback; stage dry-run identities separately and resolve dependency simulation honestly. Actual PostgreSQL parent/child simulation, repeats, wrong/stale lineage rejection/recovery and unchanged applied rows/crosswalks/progress/sequences passed in `5d682093`; zero leaked schemas/business audits. Dry Report remains simulation-only NeedsAction, not applied acceptance (SS-V054).
- [ ] T066 Build/run actual SS oracle O-1..5; compare actual Unified report/service outputs for past/cutover/future windows. Every difference maps to a known finding; unmodified adverse cases must fail (SS-V057).

## Phase 5: Verification And Evidence

- [ ] T046 Repo-local config/operations/mock runbook; mirror required workspace artifacts for versioned history.
- [ ] T047 Review projections/logging/findings/exports for credentials, photos, protected text and unsafe SQL.
- [ ] T048 Run applicable SS-V001..057; record per-ID passed/failed/pending/superseded plus exact evidence.
- [x] T049 Run smallest relevant build/tests first, PostgreSQL end-to-end/EF script/formatter and broader checks as required. SDK10 API/Runner builds, 103 migration + 5 Hangfire tests (zero skipped), actual PostgreSQL suites and format/Compose checks pass. This does not mark T048/T050 or business acceptance complete.
- [ ] T050 Review migration endpoint authorization/security before release; unresolved checks stay visible.
- [x] T067 Produce significant migration-gap report, training export-vs-SS-code comparison and recommended backlog with primary-goal impacts. Reports preserve NO-GO, BL-15 and unproved compliance/OT outcomes; no acceptance claim.
- [ ] T068 Commit logical code/test/evidence milestones without attribution; no pushes. Leave persistent mock up and record readiness/resources.

## Deferred Scope (Not Required To Finish SS Implementation)

- [ ] T024 CASS registration tests — deferred.
- [ ] T025 CASS adapter tests — deferred.
- [ ] T026 CASS contracts — deferred.
- [ ] T027 CASS job — deferred.
- [ ] T028 CASS reader — deferred.
- [ ] T029 CASS references — deferred.
- [ ] T030 CASS people/events — deferred.
- [ ] T031 CASS roster — deferred.
- [ ] T032 CASS wiring — deferred.
- [ ] T033 CASS integration — deferred.
- [ ] T041 CB6495 flags — separate deployment follow-up.
- [ ] T042 CB6495 secret references — separate deployment follow-up.
- [ ] T043 CB6495 egress — separate deployment follow-up.
- [ ] T044 CB6495 runbook — separate deployment follow-up.
- [ ] T045 CB6495 render validation — separate deployment follow-up.

## Dependencies And Milestones

T016/053/054 -> T055 -> T056/057/059; T058 depends on T057; T060 depends on T059.
T015/018 depend on the final source contract. T019/020/022/023 depend on T018/051/057/058.
T061 and T021 can proceed independently after MyTeam works. T062/063 depend on Scheduling.
T034..040/064/065 depend on split state and respective writers; T066 depends on oracle,
all writers and findings. T048/049/067/068 depend on relevant implementation and actual evidence.
No deferred CASS/GitOps task blocks this SS build.

Milestones: plan; persistent mock; split/state/MyTeam; Training/Scheduling; reports/recovery;
validity evidence. Do not check a task merely because code compiles or its expected rule
appears in prose. No pass without measured outcomes.
