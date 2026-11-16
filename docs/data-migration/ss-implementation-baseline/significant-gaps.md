# Significant SS Migration Gaps

**Baseline 2026-11-16; runtime result pending.** Extract counts below are supplied
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
| Shape validity | Default synthetic DB prior build is not proof of +/-20% production fit | Tune/measure SH buckets and report failed fit; no parity claim from row counts alone |
| JC safety | Existing app default may permit room expiry if config missing | Migration explicit-false preflight; BL-9 for app default. Matching-key JC writes accepted |

## What Can Block Acceptance

Unrecoverable required identity/authentication material, schedule business meaning or training
compliance facts; unexplained coverage/differences; failed guards/transactions; or unresolved
future actions. Secondary Stats mapping refinement alone does not block implementation.

## Runtime Findings

Pending actual migration/oracle execution. Replace this section with measured results and
links; keep accepted source baselines distinct. Failed or unavailable tests remain explicit.
