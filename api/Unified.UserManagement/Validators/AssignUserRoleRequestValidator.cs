using FluentValidation;
using Unified.Common.Helpers.Extensions;
using Unified.Common.Validation;
using Unified.UserManagement.Models;

namespace Unified.UserManagement.Validators;

public class AssignUserRoleRequestValidator : AbstractValidator<AssignUserRoleRequestDto>
{
    public AssignUserRoleRequestValidator()
    {
        RuleFor(x => x.RoleId)
            .GreaterThan(0)
            .WithErrorCode(ApiValidationErrorCodes.Required)
            .WithMessage(ApiValidationErrorCodes.Required);

        RuleFor(x => x.EffectiveDate)
            .NotEmpty()
            .WithErrorCode(ApiValidationErrorCodes.Required)
            .WithMessage(ApiValidationErrorCodes.Required);

        RuleFor(x => x.EffectiveDate)
            .Must(x => DateTimeOffsetExtensions.IsValidDateFormat(x, DateTimeOffsetExtensions.DateFormat))
            .When(x => !string.IsNullOrEmpty(x.EffectiveDate))
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .WithMessage($"EffectiveDate must be in {DateTimeOffsetExtensions.DateFormat} format");

        RuleFor(x => x.ExpiryDate)
            .Must(x => DateTimeOffsetExtensions.IsValidDateFormat(x, DateTimeOffsetExtensions.DateFormat))
            .When(x => !string.IsNullOrEmpty(x.ExpiryDate))
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .WithMessage($"ExpiryDate must be in {DateTimeOffsetExtensions.DateFormat} format");

        RuleFor(x => x)
            .Must(x =>
                string.IsNullOrEmpty(x.ExpiryDate)
                || DateOnly.ParseExact(x.ExpiryDate, DateTimeOffsetExtensions.DateFormat)
                    >= DateOnly.ParseExact(x.EffectiveDate, DateTimeOffsetExtensions.DateFormat)
            )
            .When(x =>
                DateTimeOffsetExtensions.IsValidDateFormat(x.EffectiveDate, DateTimeOffsetExtensions.DateFormat)
                && DateTimeOffsetExtensions.IsValidDateFormat(x.ExpiryDate, DateTimeOffsetExtensions.DateFormat)
            )
            .WithName(nameof(AssignUserRoleRequestDto.ExpiryDate))
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .WithMessage("ExpiryDate cannot be earlier than EffectiveDate");
    }
}
