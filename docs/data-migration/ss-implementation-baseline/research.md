# SS Migration Decisions

**Reconciled 2026-11-16.** Deep evidence stays in
[SS notes](../../documentation/data-migration/ss-job-notes/); final rules are in the
[canonical contract](./contracts/ss-mapping-contracts.md). No unverified execution claims.

| Decision | Rationale | Alternative not chosen |
|---|---|---|
| Three SS loaders plus final Report | Independent training/roster ownership, common people lineage and final cross-module readiness | One source-wide running lock would serialize unrelated loaders |
| Same SS source/crosswalk identity | IDs remain stable across phases and rollback/replay | Job-specific source names would duplicate identities |
| Fixed cutover 2026-12-01 | Stable Draft/Active/series/retained-type expiry across rehearsals/replays | Run-clock boundary would mutate schedules on later runs |
| Read-only Npgsql DTO adapter | Isolates .NET5 SS model/provider from .NET10 target writes | Referencing legacy EF context in migration module risks writes/runtime coupling |
| Existing reference add/link only | Unified/JC authority and later user work survive | Timestamp-wins reference overwrite rejected |
| Target-field fingerprint conflicts | Protects later edits without relying on UpdatedOn | Timestamp alone cannot prove target ownership |
| Full merge-group hashing | Child/member changes re-evaluate one target correctly | Changed-row-only hashing can lose unchanged members |
| Durable retry plus independent cutoff backfill | No rejected/older row disappears behind watermark | Replay overlap alone is not sufficient recovery |
| Table locking for append allocation | Conflicts with ordinary PostgreSQL app DML without app edits | Migration-only advisory lock cannot serialize unaware app writers |
| Tracked controlled training writes | Copies exact expiry/null/version with audits and FK checks | Renewal/user-creation services can invent/recalculate qualifications |
| Q-T6C labels approved | Client cleared gate 2026-11-15; exact SS labels carry business meaning | No new authoritative catalogue supplied |
| Gap-free shift merging only | Fits Unified without inventing worked gap time | Across-gap merge corrupts hours; future gaps remain human action |
| Accepted historical overtime differences | SS duplicate/overlap stored totals cannot all be recovered from valid work intervals | Faking stored totals or app calculation fixes violates scope |
| Synthetic schema plus aggregate shape | No production people/text leave source; repeatable planted cases | Pseudonymized production dump withdrawn |
| Isolated target, preserve current DB | Shared environment must not be reset | Earlier current-local-DB reset note is superseded |
| Actual report/service oracle | Tests behavior and business data, not a reimplemented formula | Raw count equality or SQL formula alone cannot prove workflow parity |
| Explicit false JC preflight, app default backlog | Satisfies migration protection within migration-only scope | No ownership schema/backfill or JC app edit |
| Preview/confirm data-loss rollback | Accepted pre-migration-state goal includes later dependent edits, with human control | Automatic destructive rollback or silent later-edit loss rejected |

## Evidence Limits And Build Checks

- SH production aggregates were supplied on 2026-11-16 (PostgreSQL 12.18). Prior notes
  saying production shape is pending are stale; synthetic fit still requires measurement.
- Existing source container/volume and generator validation are prior evidence, not proof
  of current health or production-shaped fit. Default 80-user fixture is not a fitted model.
- Source report trace uses actual SS code. Exact business-data parity and live authentication
  remain separate from visual layout and synthetic identifier persistence.
- No extension hooks file exists. Plan setup script could not run through the current
  Windows/RTK Bash path wrapper; active feature was resolved directly from
  `.specify/feature.json`. Existing plan was replaced deliberately, no branch switch.
- Implementation/runtime unknowns are explicit validation tasks, not unresolved business
  mappings. Failed evidence blocks the relevant primary goal, never becomes a default pass.
