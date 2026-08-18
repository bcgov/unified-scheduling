# ADR: Standardize validation error codes across API and frontend

- Status: Draft
- Date: 2026-08-18
- Deciders: Not identified; issue #129 was opened by @BronzBierd
- Technical area: API validation contract and frontend error presentation
- Related: [Issue #129](https://github.com/bcgov/unified-scheduling/issues/129)

## Context

Issue #129 asks whether validation should expose stable `WithErrorCode` values
that the frontend maps to user-facing text, or rely on server-generated
messages. The repository already has shared API codes and a frontend mapping,
but validators mix code-valued and human-readable `WithMessage` values. The
response contract and fallback behavior are not yet defined.

## Decision

We will retain stable validation error codes as the cross-layer contract and
have the frontend map recognized codes to user-facing messages.

## Alternatives

- Server messages only: preserves context at the source, but couples frontend
  presentation to backend wording and weakens localization.
- Codes plus server messages: preserves both forms, but requires precedence and
  response-shape rules.

## Consequences

- Benefit: Frontend behavior depends on stable identifiers rather than parsing
  prose, and common messages stay consistent.
- Trade-off: New codes require shared meaning, frontend mapping, and fallback
  behavior.
- Mitigation: Define code semantics and add API/frontend contract tests.

## Follow-up

- Define the response shape and code/message precedence.
- Decide whether all validators use shared codes and how exceptions work.
- Decide whether generated validation metadata becomes authoritative.

## References

- [Validation error codes](../../api/Unified.Common/Validation/ApiValidationErrorCodes.cs)
- [Frontend validation error mapping](../../web/src/shared/validation/validationErrors.ts)
- [End-to-end validation article](https://developersvoice.com/blog/dotnet/end-to-end-validation-net-typescript-architecture/)