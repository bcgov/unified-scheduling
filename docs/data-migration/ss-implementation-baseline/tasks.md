# Tasks: SS Migration And Validity

**Updated 2026-11-18.** [Plan](./plan.md), [spec](./spec.md),
[canonical contract](./contracts/ss-mapping-contracts.md), [backlog](./backlog.md).
Existing IDs retained. Checked foundation tasks are inherited history, not new test results.
Implementation is authorized; production writes/pushes/non-migration app changes are not.

**Current disposition:** migration implementation and focused runtime proofs are delivered;
acceptance remains NO-GO. Exact original SH SQL passes two shape mocks. BL-15 and unresolved
source/adverse/report/OT differences remain explicit. Unchecked compound tasks retain broader
verification or acceptance work; see the [measured acceptance inventory](../../unified-scheduling/docs/data-migration/reports/2026-11-16-acceptance-inventory.md).

**2026-11-18 review:** build tasks with measured v20 evidence are checked
("implemented; acceptance separate"). Remaining work is grouped in Phase 6 by owner.
Training compliance sign-off can only happen in production (T070). CASS and tenant
deployment configuration is out of scope; the GitOps runbook is in scope.

## Current Scoped Apply Work

- [x] T074 Add per-job opt-in for API-only apply, defaulting to dry-run and preserving the rehearsal-only rollback guard.
- [x] T075 Keep Report generation errors separate from loader failures, with explicit non-passing diagnostics.
- [x] T076 Align apply, rollback and Report operations documentation without enabling production configuration.
- [x] T077 Verify the apply and Report contracts, format changed code and record test evidence.

Focused checks: 14 direct safety and 31 recurring-job tests passed. Seven isolated
PostgreSQL tests passed, including default and explicit dry runs, one nonempty apply,
worker gate rechecks, Report errors and refusal of rollback on a non-rehearsal target.
No HTTP or production requests were sent. Full-chain and race coverage remains open.

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
- [x] T057 Add Job/lineage/cutover/restore-point/heartbeat or fenced-lease state, `(Source,Job)` Running guard, per-job controls and source-wide controls in migration-owned schema. Atomic dependency gate permits parallel Training/Scheduling, not parent overlap; stale-run recovery cannot allow old-worker writes. Implemented; v20 lineage gate refused Report on failed child. Exhaustive race matrix is T034.
- [x] T058 Implement typed paging upper bounds, deterministic mapped/source hashes, full merge-group reloads, durable rejected-row retry and separate resumable cutoff-backfill progress (EN-1/4/5). Implemented; PG resume/retry/backfill probes pass; v20 4h30m50s. Production sizing is T069.
- [x] T059 Inspect/start/preserve existing stopped SS mock; add isolated persistent Compose resources, named volumes, synthetic seed/schema provenance and health checks. Healthy source/target retained; original source preserved. See measured report; shape fit remains T060.
- [x] T060 Tune deterministic synthetic data from production SH-01..08; measure ±20% fit on non-suppressed comparable buckets; verify repeated-build hashes, filled-DB refusal, all extractors and manifest cases (SS-V056). Exact original SH SQL in `d3a05fe6` passes both independent shape builds: 4,631 numeric cells within bounds, 1,222 suppressed bounds satisfied, 53 production-withheld cells explicitly noncomparable; all 5,906 cells accounted. Adverse fixture is separate and correctly fails shape fit. Remaining manifest differences are explicit in the T048 ledger; the two G8 fixture expectation mismatches are accepted under T062.
- [ ] T015 Extend adapter watermark/tied-time/read-only/cutoff and backfill tests (SS-V026).
- [x] T017 Retire original MappingApprovalRequired shell; implementation now belongs to T056.
- [x] T018 Implement all required read-only source projections/schema/timezone preflight; verify actual singular SS table/column names. Actual source reads account for 101,426 selected identities.
- [ ] T051 Migration-only JC guard requires all three expiry flags explicitly false, with no business writes on failure. Old JC code-default change moved to BL-9. Validate SS-V036/038/039/048 without altering JC behavior.

## Phase 2: MyTeam

- [x] T019 Implement reference/type mappings under final contract; per-job ownership splits Type 6/0 into later jobs. Existing references unchanged, generated IDs/crosswalks, safe append locking with ordinary writers. v20: 2,233 MyTeam identities, 0 rejected/failed.
- [x] T020 Implement users/roles/UserRole/acting/leave/away with identity/auth preservation, null Gender Other, exact dates/text, no UserCreatedSignal or invented training. Training alone owns profile assignment. All users/roles/lookups migrate (not history-filtered). Real IDIR login is T073.
- [x] T022 Wire source/mappers/jobs and disabled-by-default configuration without CASS coupling.
- [x] T023 Add MyTeam PostgreSQL integration cases for idempotence, links, later-user edits, row rejection/retry and batch atomicity. Actual PG insert/replay/drift/retry/atomicity cases pass (rehearsal, recovery, rollback reports).

## Phase 3: Training And Scheduling

- [x] T061 Implement categories/courses/profile requirements/history/versioning/qualifying profiles. Preserve certificate expiry including null; exclusions/notes/notice exact; no renewal/mail side effects (EN-6). v20: 568 identities, 0 rejected/failed; stored facts match SS. Compliance sign-off is T070.
- [x] T021 Implement classifications, merged definitions/shifts, duties/slots/links and qualifying G-10 series in required order. No invented shift gap time/occurrences. v20: 98,625 identities, 0 rejected; 20,224 standalone roots kept. 2 failed outcomes are source/app issues (T071/T072).
- [ ] T062 Validate capacities, many-to-one identities, source membership changes, future Draft publish readiness, cutover-day series compatibility and retained expired leave editing (SS-V014..022/050..052). Owner decisions 2026-11-18: accepts the two G8 fixture merge mismatches as expectation errors (no mapping change); accepts the future publish preview as blocked pending T072 (not a pass). Code review confirms BL-8: leave edit forms filter by event date and clear a saved type when it is no longer active. Keep open for BL-8 and remaining coverage; see the [validation ledger](validation-ledger.md).

## Phase 4: Reports, Operations And Recovery

- [ ] T034 Add pause/resume/abort/rollback/dry-run tests for split lineage and races.
- [x] T035 Implement durable source/job controls and safe status/failure queries. Actual PostgreSQL operations proofs pass.
- [x] T036 Implement preview-first confirmed compensating rollback, dependency order, merge ownership, footprint proof and transactional failure handling (SS-V055). Graph inverse restores prior footprint; broader race/inverse coverage remains T034/T055. T050 endpoint review found and fixed feature-flag route exposure and cookie-CSRF risks; broader platform follow-ups remain in BL-18/19.
- [x] T037 Add job-scoped operations contracts/DTOs.
- [x] T038 Add existing-framework migration permission checks/endpoints; no auth framework changes.
- [x] T039 Verify authorization, source/job isolation, redaction and stable run IDs. Focused operations tests and actual PostgreSQL proofs pass; T050 completed the separate endpoint security review.
- [x] T040 Safe structured logging only. Operations findings/exports are redacted; protected rows remain restricted.
- [x] T064 Implement findings/outcome coverage catalogue, fail-closed verdicts, same-lineage final Report and CSV/Markdown exports (SS-V053). Open NeedsAction never passes. Actual ready Report fails differences; invalid adverse lineage rejects Report acquisition.
- [x] T065 Validate mode runs real DML then rollback; stage dry-run identities separately and resolve dependency simulation honestly. Actual PostgreSQL parent/child simulation, repeats, wrong/stale lineage rejection/recovery and unchanged applied rows/crosswalks/progress/sequences passed in `5d682093`; zero leaked schemas/business audits. Dry Report remains simulation-only NeedsAction, not applied acceptance (SS-V054).
- [ ] T066 Build/run actual SS oracle O-1..4; compare actual Unified report/service outputs for past/cutover/future windows. Every difference maps to a known finding; unmodified adverse cases must fail (SS-V057). O-5 overtime moved to BL-17 with T063. Planned as the next PR stack: O-1 training export vs Unified training report export, O-2..4 shifts/distribute/roster via Unified APIs, ready fixture after T072, BL-15 rows stay as known blocker.

## Phase 5: Verification And Evidence

- [x] T046 Repo-local config/operations/mock runbook; mirror required workspace artifacts for versioned history. `docker/migration/README.md`, `SHAPE-PROFILE.md`, quickstart and `ss-implementation-baseline` mirror.
- [x] T047 Review projections/logging/findings/exports for credentials, photos, protected text and unsafe SQL. 2026-11-18 scan: dynamic SQL uses fixed/model identifiers only, values parameterized; no ILogger/value logging; stored failures and job exceptions use fixed text (no inner exception); source connections set `IncludeErrorDetail=false`; exports hold IDs/codes only with CSV formula-injection guard; photos excluded from hashes/exports; Runner not in release image. Low: rehearsal-only `SafeStack` console blocks (type/SqlState/frame names only) remain in executor/write session; remove before release.
- [x] T048 Build the SS-V001..057 ledger from retained evidence; label pass/partial/failed/blocked/pending/deferred/superseded states, BL-15 blockers and the separate production-only gate. Ledger completion does not mean all tests pass or production is accepted; see the [validation ledger](validation-ledger.md).
- [x] T049 Run smallest relevant build/tests first, PostgreSQL end-to-end/EF script/formatter and broader checks as required. SDK10 API build and format checks pass. Latest DataMigration filter: 137/137 pass with PostgreSQL; without PostgreSQL, 130 pass and 7 skip. This does not close the open validations in the T048 ledger or business acceptance.
- [x] T050 Review migration endpoint authorization/security before release. Class-level `DataMigrationOperate` auth and resolved actor; rollback adds `DataMigrationRollback`; source/job scope and redacted exports; JSON-only unsafe requests (including empty-body POSTs); feature-flag-off routes return 404. Added tests cover permissions, no-content-type/form POSTs (415 before audit), and disabled routing. SDK10 DataMigration filter: 137 passed with PostgreSQL; 130 passed/7 PostgreSQL-gated skips without it. Findings: global cookie-CSRF protection and Hangfire trigger authorization remain BL-18/19; these are broader platform follow-ups, not fixed here.
- [x] T067 Produce significant migration-gap report, training export-vs-SS-code comparison and recommended backlog with primary-goal impacts. Reports preserve NO-GO, BL-15 and unproved compliance/OT outcomes; no acceptance claim.
- [x] T068 Commit logical code/test/evidence milestones without attribution; no pushes. Leave persistent mock up and record readiness/resources. Mock retained per closeout report.

## Phase 6: Release Acceptance (Remaining)

Open tasks above still apply: T015, T034, T051, T062, T066. T050 is complete; T063 moved to Out Of Scope (BL-17).

**Migration engineering (can be done now):**
- [ ] T069 Re-run v20 full chain with `HistoryStartDate=2020-01-01` on a fresh target; record counts, skips and timing at BatchSize 100-250 plus an unchanged re-run.
- T062: two G8 fixture expectation mismatches accepted; future publish preview waits on T072; BL-8 edit gap confirmed by code review and remains open.
- T063: out of release scope (owner decision 2026-11-18); revise/test overtime later as BL-17. Past differences stay as findings.
- T015/T034: complete paging/cutoff and pause/abort/rollback race test matrices.
- T048: complete; per-ID evidence ledger is recorded. Open validations remain explicitly open.
- T051: validate the JC guard (SS-V036/038/039/048).
- T047/T050: completed security and endpoint authorization reviews (2026-11-18); BL-18/19 remain broader platform follow-ups.

**External owners (block production acceptance):**
- [ ] T071 BL-15 LoanOut availability fix in Unified Scheduling (app owner). Primary blocker.
- [ ] T072 Repair four Surrey future-gap shift groups in SS before cutover (scheduler); re-run unblocks the held DutySlot and publish preview.
- [ ] T073 Verify real IDIR/Keycloak login for migrated users in dev/test (environment owner).
- T066: business accepts training status/Excused/layout report differences or raises backlog.

**Production only:**
- [ ] T070 Training compliance sign-off against production data (excused-user and deleted histories excluded; BL-1/2/13/14). Cannot be completed before production.

## Out Of Scope For This Release

- [ ] T063 Overtime verification (WorkingHoursService, 420/60/0, multi-location, O-5) — deferred to BL-17.
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
T061 and T021 can proceed independently after MyTeam works. T062 depends on Scheduling; T063 is deferred to BL-17.
T034..040/064/065 depend on split state and respective writers; T066 depends on oracle,
all writers and findings. T048/049/067/068 depend on relevant implementation and actual evidence.
No deferred CASS/GitOps task blocks this SS build.
Phase 6: T069 depends on T049; T072 precedes the final T069 re-run; T070 follows production cutover.

Milestones: plan; persistent mock; split/state/MyTeam; Training/Scheduling; reports/recovery;
validity evidence. Do not check a task merely because code compiles or its expected rule
appears in prose. No pass without measured outcomes.
