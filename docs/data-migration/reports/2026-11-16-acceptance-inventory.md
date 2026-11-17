# SS delivery inventory: implementation is not acceptance

**Production disabled. Full SS acceptance FAIL / NeedsAction. No push.**
This inventory distinguishes implemented paths, measured acceptance failures,
external-input blocks and genuinely unfinished migration work. Old planning
checkboxes are not evidence and have not been bulk-checked.

## Measured acceptance gates

| Gate | State | Evidence / precise limit |
|---|---|---|
| Persistent isolated source, original fixture preservation | Exercised | Named Docker volumes, original `ss-synth-pg` retained, no shared DB reset or host DB ports |
| SH01–08 local production-aggregate fit | Local checks pass, original-query gate blocked | 4,631 comparable +1,222 suppressed bounds +53 explicitly noncomparable =5,906 cells; exact inventory/rebuild/refusal in [shape report](2026-11-16-shape-profile.md) |
| Original production extractor SQL | **External input blocked** | Original SQL absent; no substitute-query claim. [All 53 reasons](2026-11-16-extractor-validity.md) |
| Ready-v4 source/target Report | **FAIL** | Actual matched source; 57/33 strict rows, 18 exact, 39 missing, 15 unexpected response rows. Not lost-record totals |
| Formerly unexecuted 22 rows | Actual common reads executed where possible; full contract still unexposed | 4 resource presences exact; 15 event rows: 8 exact/7 missing/1 unexpected; 3 harness projections are not UI evidence. [Methods/fields/windows](2026-11-16-actual-distribution-reads.md) |
| BL15 future scheduling | **Primary app-owned blocker** | 12 actual matched calls: 2 destination candidate-presence differences +4 missing Away conflict responses. No loss waiver or app change |
| Training facts | Narrow fixture facts match, broader acceptance unproved | Same completion/course/user/home/region/end/expiry. Three windows × `excused`, presentation variant, derived status =9 differing/unexposed cells; no broad compliance/UI claim |
| Graph replay and recovery | Implemented, selected PG cases pass | Merge/split/repartition, multi-entry duties, changed qualifying series, drift hold, interrupted batches/retry/backfill and confirmed inverse are exercised in linked milestone evidence |
| Dry-run candidate lineage | Implemented, selected PG cases pass | Durable separate namespace, real rehydration, stable repeats/wrong/stale rejection and rollback footprint; not applied lineage |
| Full valid-parent adverse execution | **Executed; acceptance FAIL** | 101,426 selected identities exactly accounted; Scheduling98,625/33rejected/2failed; all3,388 manifest rows classified. [Full outcome/exception table](2026-11-16-adverse-valid-parent.md) |
| Full release security/concurrency/validation register | **Incomplete** | Foundation71 +Hangfire5 are not the complete PostgreSQL or SS-V001..057 suite |
| Latest workspace canonical mirror | **External coordination pending** | Parent owns workspace documents; refreshed repo-local canonical copies have not been supplied. Existing mirrors are historical, not claimed current |

## Complete task inventory by actual implementation state

“Implemented” below is about code paths, not a blanket acceptance/test pass.

| Task IDs | Implementation state | Acceptance / work still required |
|---|---|---|
| T001–014 | Inherited module/schema/primitives/foundation tests present; split registration exercised | Broader integration cases do not follow from foundation tests |
| T016, T053, T054 | Initial canonical planning reconciliation/mirror present | Final parent-owned canonical refresh remains separate |
| T052 | Separate UpdatedOn workflow, not changed here | Outside this implementation |
| T055, T046, T068 | Repo-local runbooks/evidence/logical commits and preserved resources exist | Final workspace mirror synchronization pending supplied canonical inputs; no push |
| T056, T017, T022 | Four jobs/per-job options/ownership, old shell retirement, disabled defaults implemented | CASS unchanged/disabled; no production activation |
| T057 | Job/lineage/cutover/fencing/controls/advisory acquisition and Running guard implemented | Complete independent race/stale-worker matrix not executed |
| T058 | Typed bounded source paging, full-group hashes, durable retries/checkpoints and separate backfill implemented | Full-source rescans/group planning and repeated initial crosswalk capture reads remain performance work; full adverse Scheduling took5h24m, not production sizing acceptance |
| T015 | Focused adapter/recovery cases exist | Exhaustive PostgreSQL tie/paging/cutoff/backfill matrix incomplete |
| T059 | Persistent isolated mock/target Docker resources implemented and exercised | Original sources/failures/volume retained; full untrimmed valid-parent clone executed all selected physical identities |
| T060 | Deterministic production-aggregate generation/local measurement/rebuild/refusal implemented | Original SH/EX/AG/AD/SP/OD queries missing; full task not accepted |
| T018, T051 | Actual source projections/read-only snapshot/preflight and explicit-false JC guard implemented | No app JC-default edit, no production preflight acceptance |
| T019, T020 | Primary MyTeam references/users/roles/UserRole/acting/leave/current LoanOut writers implemented | Real SSO login, all retained-reference/identity/drift variants not independently certified |
| T023 | Selected actual PG insert/replay/drift/retry/atomicity cases exercised | Comprehensive independently maintained integration suite incomplete |
| T061 | Categories/courses/profiles/history/requirements/exclusions implemented | BL13/BL14/true-Excused/broad compliance/date-display acceptance unresolved |
| T021 | Scheduling definitions/shift merges/duties/slots/links/qualified series implemented | No synthetic occurrences; unsupported patterns remain standalone |
| T062 | Required repartition/multi-entry/changed-series behavior implemented and exercised | Full adverse graph/cancellation/counter/rollback proofs passed, but33syntheticseries rejected inconsistent members; qualification/fallback suitability and2fixturemerge expectations unresolved |
| T063 | Actual WorkingHours/publish/calendar services run; differences retained | All 43 planted logical SheriffDay overtime expectations lack independent matched actual-service proof; no source formula substitute |
| T034 | Control/recovery/rollback machinery exists; selected failures exercised | Actual valid-parent Running/Failed child Report acquisition guards passed; exhaustive pause/abort/dependency races remain unimplemented test coverage |
| T035 | Source/job control service and CLI/state reads implemented | Not an authorized HTTP operations surface |
| T036 | Encrypted inverse journal, immutable preview/export/digest, confirmation/dependency guards and atomic inverse implemented | Report reversal, broader races/adverse inverse and prior nonnull private-photo restoration remain uncovered/blocked; production apply disabled |
| T037 | Written operations contract and service/CLI exist | Public job-scoped operations DTO/API integration incomplete |
| **T038** | **Unimplemented: no migration controllers/HTTP endpoints or migration permission enforcement** | Must use existing auth framework; missing HTTP authorization is not an app-model blocker |
| T039, T050 | Stable IDs, source/job guards and safe diagnostic paths exist | Endpoint authorization/security acceptance unimplemented because T038 is missing; full release review incomplete |
| T040, T047 | Safe structured findings, mapped cryptographic hashes, private artifact boundaries and restricted runner audit configuration implemented | Independent comprehensive security review not claimed; no photos/credentials/raw personal text in committed evidence |
| T064 | Coverage ledger, exclusions, primary failure holds and same-lineage Report implemented | Complete operator-facing findings CSV/Markdown export/API still incomplete; JSON/CLI receipts and authored Markdown evidence are not that endpoint |
| T065 | Durable dry candidates/actual DML/rehydration/rollback implemented and exercised | Broad overlapping applied-target/update-preview equivalence not proven |
| T066 | Independent actual SS captures and actual target service comparisons executed | Strict Report fails; UI/CSV/PDF not executed; no recreated SS algorithms to force pass |
| T048 | Concrete milestone evidence exists | Complete authoritative per-SS-V001..057 verdict ledger not finished; superseded040/041 must not become false missing ownership work |
| T049 | SDK10 build/focused suites/formatter/Compose/operational PG checks executed | Not a whole-repository test pass or exhaustive migration validity suite |
| T067 | Significant migration-gap/paired-field/training-fact/backlog evidence authored | Business/app owners must decide measured model/API gaps; no automatic waiver |
| T024–033 | Deferred CASS, intentionally not implemented | Not an SS build blocker |
| T041–045 | Deferred tenant GitOps/deployment work | Unified-Architect → Unified-DevOps-Lead only after acceptance; no tenant files changed |

## Owner decisions versus remaining engineering

1. **Scheduling/LoanOut owner:** BL15 needs interval-sensitive borrowed-user discovery,
   LoanOut participation in proposed-shift conflicts, and aligned assignment guards.
   Historical destination-view event exposure also differs. These are actual service
   semantics; cloning/widening migration events is not an acceptable workaround.
2. **Training/business owner:** decide future expiry-policy/date/Excused and presentation
   requirements using actual model/API evidence. Do not equate stored completion facts
   with all compliance/UI behavior.
3. **Input/canonical owner:** supply original extractor SQL and final workspace mirrors.
   No exact-original-query or current-mirror claim until those inputs are received.
4. **Migration engineering still needed:** T037/038 operations API/permission integration,
   complete T064 findings exports, the missing independent release/security/concurrency/
   inverse/OT/SS-V coverage. These are genuinely unfinished work, not external blockers
   and not silently declared complete because BL15 independently prevents acceptance.
5. **Adverse suitability/performance:** all selected physical rows now executed, not
   3,377 pending downstream rows. Retain the actual mismatches:30 differing outcome
   labels,2 fixture merge mismatches,43 logical OT expectations without independent
   proof, and10 cases superseded only in the repaired clone. Resolve synthetic-series
   fallback, the material missing link/future publish blocker, and source/target
   full-capture query scaling; do not turn complete accounting into business acceptance.

No bc-design-agent/design-system question arose. No deployment/environment/port change
was made for real tenants. Existing deleted draft mapping contract stays unstaged.
