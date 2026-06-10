using FluentValidation;
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
            .NotEqual(default(DateTimeOffset))
            .WithErrorCode(ApiValidationErrorCodes.Required)
            .WithMessage(ApiValidationErrorCodes.Required);

        RuleFor(x => x.ExpiryDate)
            .NotEqual(default(DateTimeOffset))
            .When(x => x.ExpiryDate.HasValue)
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .WithMessage(ApiValidationErrorCodes.Invalid);

        RuleFor(x => x)
            .Must(x => !x.ExpiryDate.HasValue || x.ExpiryDate.Value >= x.EffectiveDate)
            .WithName(nameof(AssignUserRoleRequestDto.ExpiryDate))
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .WithMessage(ApiValidationErrorCodes.Invalid);
    }
}
