using FluentValidation;
using Unified.Common.Validation;
using Unified.UserManagement.Models;

namespace Unified.UserManagement.Validators;

public class RoleRequestValidator : AbstractValidator<RoleRequestDto>
{
    public RoleRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithErrorCode(ApiValidationErrorCodes.Required)
            .MaximumLength(100)
            .WithErrorCode(ApiValidationErrorCodes.TooLong);

        RuleFor(x => x.Description)
            .NotEmpty()
            .WithErrorCode(ApiValidationErrorCodes.Required)
            .MaximumLength(500)
            .WithErrorCode(ApiValidationErrorCodes.TooLong);
    }
}
