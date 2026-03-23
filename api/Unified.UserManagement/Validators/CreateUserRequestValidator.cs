using FluentValidation;
using Unified.Common.Validation;
using Unified.UserManagement.Models;

namespace Unified.UserManagement.Validators;

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.IdirName)
            .NotEmpty()
            .WithErrorCode(ApiValidationErrorCodes.Required)
            .WithMessage(ApiValidationErrorCodes.Required)
            .MaximumLength(200)
            .WithErrorCode(ApiValidationErrorCodes.TooLong)
            .WithMessage(ApiValidationErrorCodes.TooLong);

        RuleFor(x => x.FirstName)
            .NotEmpty()
            .WithErrorCode(ApiValidationErrorCodes.Required)
            .WithMessage(ApiValidationErrorCodes.Required)
            .MaximumLength(150)
            .WithErrorCode(ApiValidationErrorCodes.TooLong)
            .WithMessage(ApiValidationErrorCodes.TooLong);

        RuleFor(x => x.LastName)
            .NotEmpty()
            .WithErrorCode(ApiValidationErrorCodes.Required)
            .WithMessage(ApiValidationErrorCodes.Required)
            .MaximumLength(150)
            .WithErrorCode(ApiValidationErrorCodes.TooLong)
            .WithMessage(ApiValidationErrorCodes.TooLong);

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithErrorCode(ApiValidationErrorCodes.Required)
            .WithMessage(ApiValidationErrorCodes.Required)
            .MaximumLength(320)
            .WithErrorCode(ApiValidationErrorCodes.TooLong)
            .WithMessage(ApiValidationErrorCodes.TooLong)
            .EmailAddress()
            .WithErrorCode(ApiValidationErrorCodes.InvalidEmail)
            .WithMessage(ApiValidationErrorCodes.InvalidEmail);

        RuleFor(x => x.Rank)
            .NotEmpty()
            .WithErrorCode(ApiValidationErrorCodes.Required)
            .WithMessage(ApiValidationErrorCodes.Required)
            .MaximumLength(150)
            .WithErrorCode(ApiValidationErrorCodes.TooLong)
            .WithMessage(ApiValidationErrorCodes.TooLong);

        RuleFor(x => x.BadgeNumber)
            .NotEmpty()
            .WithErrorCode(ApiValidationErrorCodes.Required)
            .WithMessage(ApiValidationErrorCodes.Required);

        RuleFor(x => x.HomeLocationId)
            .NotNull()
            .WithErrorCode(ApiValidationErrorCodes.Required)
            .WithMessage(ApiValidationErrorCodes.Required);
    }
}
