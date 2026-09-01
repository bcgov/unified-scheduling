# ADR: Standardize validation error codes across API and frontend

- Status: Accepted
- Date: 2026-08-18
- Deciders: Not identified; issue #129 was opened by @BronzBierd
- Technical area: API validation contract and frontend error presentation
- Related: [Issue #129](https://github.com/bcgov/unified-scheduling/issues/129)

## Context

Issue #129 captured a contract that is now in use. The API defines shared
validation codes, groups validation failures by property, and sends error codes
in `ValidationProblemDetails.Errors`. The frontend maps those codes to display
messages and currently passes an unmapped code through as fallback text.
Adoption is inconsistent: some standard rules duplicate a code in
`WithMessage`, while others still use free-text messages without an error code.
Full validation-rule generation through OpenAPI, Orval, and Zod is out of
scope; this decision covers the error-code contract.

## Decision

We will use the shared `ApiValidationErrorCodes` values as the API/frontend
contract. Standard FluentValidation rules must use `WithErrorCode(...)` and
must not also use `WithMessage(...)`. The frontend owns user-facing message
text and new error codes must be added to its mapping in the same change.

Rules for specialized business logic may use `WithMessage(...)` when a
non-standard message is required; this is an explicit exception, not the
default validation pattern.

## Alternatives

- Server messages only: preserves context at the source, but couples frontend
  presentation to backend wording and weakens localization.
- Codes plus messages for every rule: preserves both forms, but duplicates the
  contract and requires precedence rules.

## Consequences

- Benefit: Frontend behavior depends on stable identifiers, while specialized
  rules can still provide meaningful context.
- Trade-off: Existing validators need migration, and unmapped codes currently
  produce inconsistent raw-code text.
- Mitigation: Add every new code to the frontend map in the same change and
  test known and unknown code behavior.

## Follow-up

- Remove redundant `.WithMessage(ApiValidationErrorCodes...)` calls from
  standard rules.
- Migrate standard validators that still use free-text messages, including
  `UserRequestValidator`, and classify legitimate specialized-message cases.
- Keep the OpenAPI-to-Orval-to-Zod validation metadata pipeline as a separate
  future decision.

## References

- [Validation error codes](../../api/Unified.Common/Validation/ApiValidationErrorCodes.cs)
- [API validation exception handling](../../api/Unified.Infrastructure/ErrorHandling/GlobalExceptionHandler.cs)
- [Frontend validation error mapping](../../web/src/shared/validation/validationErrors.ts)
- [User validation rules](../../api/Unified.UserManagement/Validators/UserRequestValidator.cs)
- [End-to-end validation article](https://developersvoice.com/blog/dotnet/end-to-end-validation-net-typescript-architecture/)