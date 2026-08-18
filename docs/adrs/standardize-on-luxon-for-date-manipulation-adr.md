# ADR: Standardize on Luxon for frontend date manipulation

- Status: Draft
- Date: 2026-08-18
- Deciders: Not identified; issue #132 was opened by @BronzBierd
- Technical area: Web frontend date and time handling
- Related: [Issue #132](https://github.com/bcgov/unified-scheduling/issues/132)

## Context

The frontend already depends on Luxon, uses its Vuetify adapter, and relies on
`web/src/utils/date.ts` for parsing, formatting, and timezone conversion. Issue
#132 asks that this existing practice be recorded. The application handles
date-only values, local date-times, offsets, and IANA zones, but the convention
for handling those values and the long-term library choice are not yet agreed.

## Decision

We will use Luxon for frontend date parsing, formatting, timezone conversion,
and date arithmetic, favoring shared behavior in `web/src/utils/date.ts`.
Server-side date types are out of scope.

## Alternatives

- Native `Date` and `Intl`: avoids a dependency, but requires more project-owned
  rules for parsing and timezone manipulation.
- Temporal: offers stronger date types, but introduces a second model and a
  migration decision while Luxon is already integrated.

## Consequences

- Benefit: Frontend date behavior has one established library and integration.
- Trade-off: The dependency can still be bypassed, causing inconsistent zone
  handling.
- Mitigation: Document value categories and test offsets and daylight-saving
  transitions in the shared utility.

## Follow-up

- Define canonical handling for date-only, local, instant, and zoned values.
- Decide whether direct library usage should migrate to shared utilities.
- Decide when, if ever, Temporal should replace Luxon.

## References

- [Frontend package manifest](../../web/package.json)
- [Shared date utility](../../web/src/utils/date.ts)
- [Vuetify configuration](../../web/src/plugins/vuetify.ts)