# Significant SS Migration Gaps

**Baseline 2026-11-16; initial runtime Fail / NeedsAction.** Extract counts below are supplied
production evidence, not measurements from this session or hardcoded synthetic pass limits.

| Area | Finding / impact | Disposition |
|---|---|---|
| Future shift gaps | Four Surrey user/location/date groups, eight shifts, gaps 135-250 min. Unified cannot publish these as separate same-day shifts | NeedsAction: fix in SS or rebuild with scheduler approval. Do not merge unworked gap time |
| Future schedule visibility | Migrated dates after 2026-12-01 are Draft, not staff-visible | Preview/publish per location/date before use. SP-04 baseline 12 calendar conflicts (11 away, 1 training) |
| Past overtime | 84 days, Unified minus SS = -388.3h. 34 gap days -45.8h, one overlap -8h, 49 stored-exceeds-rule days -334.5h | Accepted historical difference; one finding per day, exact metrics. BL-4/6, no app fix now |
| No-shift filled slots | 42 past slots / 0.064% baseline | Approved drop of user link; entry remains unfilled. >0.1% OR any future case blocks acceptance |
| Leave waitlist/test | Excluded assignments and dependants; ~650 waitlist duties, one filled | Approved exclusion with counts/IDs; BL-3 |
| Excused training | Excused users migrate but all their training is excluded, even if enabled | Approved exclusion/counts; compliance comparison must distinguish SS API grouping from CSV exclusion. BL-2 |
| Deleted training | Individual soft-deleted history has no Unified inactive marker | Approved exclusion, not certificate expiry. Preserve old targets/warn if deletion occurs after migration |
| Training statuses | SS grace-year/report-history quirks differ from Unified | Preserve dates/business facts; measure actual export differences; BL-1/7/11 |
| Training timezone | Source history timezone has no UserTrainings counterpart | Check actual completion/expiry business-date exports; unverified impact BL-14, not a confirmed loss yet |
| Expired leave edit | 37 retained types expire at cutover but linked future leave remains | Verify picker/edit behavior; potential workflow impact BL-8, no silent type replacement |
| Login | Source may supply IdirName/IdirId/KeyCloakId, not a transferable identity-provider session | Preserve available unique IDs; actual IDIR/realm login needs environment check. Never claim synthetic persistence proves login |
| Shape validity | Exact original SH SQL passes both v10 shape builds: 4,631 numeric cells, 1,222 suppressed bounds and 53 production-withheld/noncomparable cells account for all 5,906 | Shape gate passed; no missing/duplicate/unexpected buckets. Separate adverse fixture correctly fails shape comparison and is not production-shaped |
| JC safety | Existing app default may permit room expiry if config missing | Migration explicit-false preflight; BL-9 for app default. Matching-key JC writes accepted |
| Partial-day LoanOut | Future matched services differ on two destination candidate responses and omit four Away conflict responses across 12 calls; original two-hour calendar data remains | Confirmed scheduling-service parity gap, BL-15. Blocks production acceptance pending an approved app fix/operational resolution; no migration-side interval distortion |

## What Can Block Acceptance

Unrecoverable required identity/authentication material, schedule business meaning or training
compliance facts; unexplained coverage/differences; failed guards/transactions; or unresolved
future actions. Secondary Stats mapping refinement alone does not block implementation.

## Latest Runtime Findings

The measured rehearsal (local-only: `2026-11-16-rehearsal.md`)
is not accepted, but initial mapper/constructor gaps have been fixed. Ready-v4 loaders
complete 41/10/23 rows with no rejected/failed; actual LoanOut/recurrence/publication paths run.
71 migration + 5 Hangfire tests pass. Latest matched Report covers 74 source records and
is FAIL: 57 source/33 target response rows, 39 missing/15 unexpected comparison rows.
These are not lost-record counts; per-surface business-impact classification is pending.

Durable bounded target batches, interruption/resume, retry and earlier-cutoff backfill now
pass PostgreSQL probes. An encrypted inverse journal, confirmed rollback digest and dependency
guards now exist; inverse/reapplication probes pass, but full rollback coverage is unfinished.
Multi-root repartition/multi-entry Duty/changed-series replay and preview-confirmed inverse
now pass PostgreSQL probes, including history/drift checks and exact prior footprint restoration.
Dry-run parent/child candidate staging now passes PostgreSQL repeat/lineage/recovery tests
without changing applied state or leaking schemas/business audits. Broader coverage remains
unfinished; production apply stays disabled.
Actual source oracle passed independently, but +/-20% shape fit and matched cross-app
business-data parity remain pending. Target day-based LoanOut probes suppress 1,320 minutes
outside a two-hour interval. Matched ready-v4 actual SS probes now show no home conflicts
before/after and preserve only the two-hour Away interval in a whole-day response.
Raw SS results therefore do not support whole-day home suppression. Compare equivalent
target queries before declaring the precise workflow impact; future source probe remains
pending. No UI/free-minute parity is claimed. See BL-15.

Matched target availability now quantifies two candidate-presence and four conflict-response
differences across 12 windows; exact two-hour calendar data survives. Future source execution
passes its PostgreSQL test and fresh replay: raw SS home responses have no outside
conflicts and whole-day conflicts retain only Dec 10 10:00-12:00 UTC. Matched future
target comparison now confirms the same two destination-presence/four missing-conflict
differences. BL-15 is a primary scheduling-service parity blocker, not calendar-data loss.

Matched Report discrepancies classify as 15 paired value differences, two merged aliases
and 22 unexecuted-surface rows. Training completion facts match in this fixture, but
derived compliance status and Excused exposure do not establish full compliance-export parity.
Paired response counts are slots 6/24 differing field cells, shifts 6/19 and training 3/9.
SH fit and full adverse rehearsal remain required.

V10 now passes independent shape measurements and repeat-build/refusal checks; original
extractor verification remains open. Adverse accounting covers 2,233 exact source identities
and 48 expected rejects, with 11 planted outcomes matched. The 3,377 downstream cases
blocked by invalid-parent lineage remain unexecuted, not accepted; a separate valid-parent
fixture is required.

Full valid-parent execution now accounts for 101,426 selected identities, including 98,625
Scheduling rows. Scheduling remains Failed with 33 synthetic-series rejects, one failed
DutySlot and a blocked publish preview; Report acquisition rejects. All 3,388 manifest rows
are classified, but 30 outcome differences, two merge mismatches and 43 unverified overtime
expectations remain alongside matched proofs and superseded cases.

Actual common-service reads now cover all 19 distribution observations. Exact original SH
SQL execution is pending, but its source file exists and has been located. Operations
API/authorization, complete findings exports, broader coverage, series suitability and
capture-query scaling are still implementation gaps; BL-15 is not used to conceal them.

V20 follow-up implements operations API/authorization, complete redacted findings exports
and indexed capture; actual PostgreSQL operations/replay/drift/100k-alias checks pass.
The 33 former series rejects now retain 20,224 real standalone Duty roots. Latest Scheduling
accounts for 98,625 rows with zero rejected and two failed: a DutySlot with no admitted
shift/user link because both source shifts need future-gap repair, and blocked publish preview.
The earlier implementation-gap list is superseded by these measured fixes, not waived.

101,426 total selected identities and all 3,388 manifest rows are accounted; 30 differing
outcomes, two merge mismatches and 43 unverified overtime expectations remain unapproved.
The 4h30m50s run is not production-sizing acceptance. Exact original SH SQL execution,
broader release validation and final mirror refresh remain pending; BL-15 remains primary.

Exact original SH execution now passes on both independent shape builds (`d3a05fe6`);
the earlier pending-SQL statements are superseded. All 5,906 cells reconcile, including
53 deliberately withheld production measures. The original-query gate is not a remaining
blocker. Adverse shape failure is retained, separate from the production-fit mock.

## Final Persistent Handoff

The [closeout](../../unified-scheduling/docs/data-migration/reports/2026-11-17-closeout.md)
records implementation delivered, acceptance **BLOCKED / NO-GO**. One unsupported inverse
is explicit: restoring a replaced preexisting nonnull photo. Rollback blocks that case rather
than claiming restoration; BL-16 recommends protected restoration support. Broader release
coverage also remains open.

Production-shaped databases `ss_shape_v10` and `ss_shape_v10_rebuild` are retained in healthy
`unified-ss-migration-shape-source-1`, volume `unified-ss-migration_source-shape-v7`.
Both synthetic markers/readiness probes and read-only reader permissions pass; each retains
873 users, 115,433 shifts and 101,904 duties. No host ports are published; restart policy is
`unless-stopped`. See safe shape access (local-only: `SHAPE-PROFILE.md`).
