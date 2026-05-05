using FluentValidation;
using Unified.Common.Validation;
using Unified.UserManagement.Models;

namespace Unified.UserManagement.Validators;

public class UpdateRoleRequestValidator : AbstractValidator<UpdateRoleRequestDto>
{
    public UpdateRoleRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).WithErrorCode(ApiValidationErrorCodes.Required);

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
