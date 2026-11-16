# Unified Follow-Ups From SS Migration

**Updated 2026-11-16.** No item authorizes app changes in this migration build.
Primary gates are user identity/auth data, scheduling and training business data.
Approved historical differences do not become silently fixed data.

| ID | Recommendation | Business impact / acceptance | Source / owner |
|---|---|---|---|
| BL-1 | Review SS extra year between certificate expiry and alert with Josh | Decide intended Unified report/notice status; exact stored certificate expiry must remain unchanged | Q-T6R; Unified Training |
| BL-2 | Add explicit training-report grouping/exclusion for excused sheriffs | SS Excused users migrate but their training is intentionally excluded. Show this gap in compliance comparisons; no inferred exemption | Q-E9; Unified Training |
| BL-3 | Consider draft/waitlist leave requests | Approved Leave Waitlist/Test assignment exclusions lose waitlist workflow (~650 waitlist duties in extract); preserve source counts/IDs in findings | G-3; Unified Leave |
| BL-4 | Calculate overtime per user/local day from union of shifts across all locations, one lunch/day, then 420-minute threshold | Never count gaps/overlaps twice. Decide split-shift support. Load all day shifts before location filter and attribute results afterwards; test gap/overlap/location queries | G-19 accepted 2026-11-16; Unified Scheduling |
| BL-5 | Add definition display order if needed | SS lookup sort order intentionally omitted; do not invent migration ordering | G-14; Unified Scheduling |
| BL-6 | Historical overtime adjustment/reconciliation, subject to payroll decision | 84 past days differ by -388.3h: 34 gap (-45.8), 1 overlap (-8), 49 stored-exceeds-rule (-334.5). All require findings now. Do not reproduce inflated stored hours without approval | G-19 accepted 2026-11-16; Unified Scheduling |
| BL-7 | Resolve AwardedOn/EndingOn meaning and expiry-base naming inconsistency | Historical Start/End copy is fixed; future calculations/reports must use a clearly defined business date | Q-E6; Unified Training |
| BL-8 | Keep expired selected leave type usable when editing an existing leave | Retained 37 types expire at cutover. Existing events remain, but picker may clear their type; verify future leave edit behavior and flag any workflow loss | SS-V015; Unified Leave |
| BL-9 | Make JC ExpireCourtRooms code default false | Migration preflight requires explicit false for all three flags. App-default fix stays outside this migration-only request; no ownership column/backfill | T051 scope change; Unified JC |
| BL-10 | Consider validated, reviewed manual crosswalk import | Recover genuinely ambiguous identities without guessing target IDs; separate validation/apply, source/target checks, checksum, authorization and audit | Existing plan recommendation; Unified Migration |
| BL-11 | Make training report retain any missing compliance business fields | Compare actual SS export fields and Unified report output; attach measured missing fields/affected counts, not layout differences. Primary-goal blocker only if required business data cannot be recovered | SS-V057/O-1; Unified Training |
| BL-12 | Verify assignment comments appear in calendar/read/export paths | Duty.Comment maps to Event.Description; if UI hides it, report visibility gap without app edits | G-15; Unified Scheduling |

## BL-6 Proposed Follow-Up Design (Not Implemented)

The [worksheet details](../../documentation/data-migration/ss-job-notes/mapping-decision-worksheet.md#bl-6-implementation-details-historical-overtime-reconciliation)
remain the deeper reference. Proposed `LegacyOvertimeAdjustment`: user/local date/source
unique, optional location, signed minutes, reason (GapDay/CrossLocationOverlap/StoredExceedsRule),
source shift IDs, generated ID/audits, read-only API/UI. Source key `user|localDate`.
Report rule overtime separately from legacy-adjusted totals; no future adjustment.
Rollback removes only owned adjustments. If BL-4 ships first, recalculate residual deltas.
Product owner/payroll must decide whether SS overlap/duplicate inflation should be preserved.

## Operational Actions (Not App Backlog)

- Repair/hold the four future Surrey gap groups (eight shifts, extract baseline); no merge
  across unworked gaps and no forced publish.
- Preview/publish future Draft schedules per location/date before staff need them.
  SP-05 baseline: Surrey only, 2,854 shifts/58 sheriffs to 2027-05-07.
  SP-04: 12 calendar conflicts (11 away, 1 training); require operator review.
- Reconcile all 84 historical overtime days with restricted source IDs and metrics.
- Confirm actual IDIR/Keycloak environment linkage before cutover; mock data cannot prove login.
- Provision read-only source secrets, target backup, explicit JC flags and disabled job config.
  Production writes/deployment/GitOps remain separately approved.
- Complete any failed ±20% synthetic shape buckets before claiming production-shaped parity.
