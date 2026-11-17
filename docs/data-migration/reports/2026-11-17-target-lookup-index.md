# Migration metadata reverse-lookup index

The full v18 profile exposed a second target metadata scaling defect beyond the
repeated initial-capture transfer: aliases are repeatedly found by
`Source, TargetType, TargetKey`, but only `Source, EntityType, LegacyId` was indexed.
A live reverse/fingerprint lookup sampled at 49ms while PostgreSQL dominated CPU.
This is migration-owned schema work, not an app-model blocker.

EF-generated migration `20261117095231_AddSsCrosswalkTargetLookup` adds **only**
the nonunique reverse index on `DataMigrationRecords`. Down removes only that
index. The primary source identity uniqueness remains unchanged; multiple source
aliases may still resolve to one generated target. No business ownership column,
application workflow, source data or tenant deployment setting changed.

## Actual PostgreSQL regression

Fresh isolated `unified_ss_rehearsal_adverse_valid_v20`, actual schema migration:

- Inserted 100,000 metadata-only probes inside one rollback-only transaction.
- Ten aliases match the same reverse target.
- The default actual query used the new index: **12 shared-hit blocks, 0.056ms**.
- The explicitly forced sequential **counterfactual** selected the same ten
  aliases: **1,924 shared-hit blocks, 18.927ms**.
- All 100,000 probes rolled back before genuine MyTeam staging; no probe outcome
  or padding row is part of migration accounting. Metadata identity sequence
  advancement is intentional, not business data.

This is an actual query-plan regression, not a production benchmark or business
acceptance. Reproducer: `docker/migration/verify-target-lookup-v20.sql`, refusing
any other database or preexisting crosswalk.

The v18 profiling attempt was aborted through the actual audited operations
control, with physical business/applied/progress/sequence/audit rollback checks
passing. Its final completeness assertion correctly failed. It remains an
interrupted attempt, not additional full adverse coverage; v13 remains the
retained full historical failure.

Fresh v20 uses the source-identical indexed v18 clone, BatchSize10, initial capture
membership index and this reverse target index. Genuine MyTeam
`0c285a9b-8444-4525-9637-6fb654bf8d73` completed2,233 with zero rejects/failures.
Full child results are now [recorded after completion](2026-11-17-adverse-v20-final.md):
98,625 Scheduling identities, zero rejected/two failed; full acceptance NO-GO.

## Checks

- SDK10 runner/API builds pass (zero errors).
- **103 migration +5 Hangfire tests pass, zero failures/skips.**
- Two new schema regressions verify nonunique alias lookup, retained primary
  uniqueness and exact Up/Down migration operations.
- Generated model snapshot diff is only the new index.

Production remains disabled; BL15 and all unwaived adverse/business differences
remain independent acceptance failures.
