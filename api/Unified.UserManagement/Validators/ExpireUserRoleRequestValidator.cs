using FluentValidation;
using Unified.Common.Validation;
using Unified.UserManagement.Models;

namespace Unified.UserManagement.Validators;

public class ExpireUserRoleRequestValidator : AbstractValidator<ExpireUserRoleRequestDto>
{
    public ExpireUserRoleRequestValidator()
    {
        RuleFor(x => x.RoleId)
            .GreaterThan(0)
            .WithErrorCode(ApiValidationErrorCodes.Required)
            .WithMessage(ApiValidationErrorCodes.Required);

        RuleFor(x => x.ExpiryReason)
            .NotEmpty()
            .WithErrorCode(ApiValidationErrorCodes.Required)
            .WithMessage(ApiValidationErrorCodes.Required);

        RuleFor(x => x.ExpiryReason)
            .Must(reason => UserRoleExpiryReasonCodes.All.Contains(reason))
            .When(x => !string.IsNullOrWhiteSpace(x.ExpiryReason))
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .WithMessage(ApiValidationErrorCodes.Invalid);
    }
}
