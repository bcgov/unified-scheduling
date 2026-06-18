---
name: fluent-validation-inline-messages
description: "Refactor FluentValidation validators to use inline WithMessage strings instead of WithErrorCode + shared error-code constants. Use when asked to remove error codes, inline validation messages, or clean up validators that reference ApiValidationErrorCodes."
---

# FluentValidation — Inline Messages

## When to Use
- Asked to "remove error codes" or "use inline messages" on a FluentValidation validator
- A validator uses `WithErrorCode(ApiValidationErrorCodes.*)` followed by `WithMessage(ApiValidationErrorCodes.*)`
- Migrating away from shared error-code constants to plain descriptive messages

## Rules
1. **Remove** every `.WithErrorCode(...)` call — do not replace, just delete the line.
2. **Replace** `.WithMessage(ApiValidationErrorCodes.*)` with a plain English string describing the rule.
3. **Remove** the `using Unified.Common.Validation;` import if `ApiValidationErrorCodes` is no longer referenced.
4. Keep `.When(...)` clauses and all other validation logic unchanged.
5. End every message with a period for consistency.

## Message Conventions

| Rule | Message pattern |
|------|----------------|
| `NotEmpty` / `NotNull` | `"{PropertyName} is required."` |
| `MaximumLength(n)` | `"{PropertyName} must not exceed {n} characters."` |
| `Must(...)` date format | `"{PropertyName} must be in yyyy-MM-dd format."` |
| `Must(...)` cross-field | Describe the business rule, e.g. `"ExpiryDate must be on or after EffectiveDate."` |

## Before / After Example

**Before:**
```csharp
using Unified.Common.Validation;

RuleFor(x => x.PositionTypeCode)
    .NotEmpty()
    .WithErrorCode(ApiValidationErrorCodes.Required)
    .WithMessage(ApiValidationErrorCodes.Required)
    .MaximumLength(50)
    .WithErrorCode(ApiValidationErrorCodes.TooLong)
    .WithMessage(ApiValidationErrorCodes.TooLong);
```

**After:**
```csharp
RuleFor(x => x.PositionTypeCode)
    .NotEmpty()
    .WithMessage("PositionTypeCode is required.")
    .MaximumLength(50)
    .WithMessage("PositionTypeCode must not exceed 50 characters.");
```

## Checklist
- [ ] All `WithErrorCode(...)` lines removed
- [ ] All `WithMessage(ApiValidationErrorCodes.*)` replaced with descriptive strings
- [ ] `using Unified.Common.Validation;` removed (if unused)
- [ ] No other logic changed (`.When`, `.Must`, rule ordering)
- [ ] Build passes after changes
