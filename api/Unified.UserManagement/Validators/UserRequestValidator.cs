using FluentValidation;
using Unified.Common.Validation;
using Unified.UserManagement.Models;

namespace Unified.UserManagement.Validators;

public class UserRequestValidator : AbstractValidator<UserRequestDto>
{
    public UserRequestValidator()
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
            .WithMessage(ApiValidationErrorCodes.InvalidEmail)
            .When(x => x.Email is not null);

        RuleFor(x => x.Gender)
            .IsInEnum()
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .WithMessage(ApiValidationErrorCodes.Invalid);

        RuleFor(x => x.HomeLocationId)
            .GreaterThan(0)
            .WithErrorCode(ApiValidationErrorCodes.Required)
            .WithMessage(ApiValidationErrorCodes.Required);

        RuleFor(x => x.Rank)
            .NotEmpty()
            .WithErrorCode(ApiValidationErrorCodes.Required)
            .WithMessage(ApiValidationErrorCodes.Required)
            .MaximumLength(150)
            .WithErrorCode(ApiValidationErrorCodes.TooLong)
            .WithMessage(ApiValidationErrorCodes.TooLong);

        RuleFor(x => x.BadgeNumber)
            .MaximumLength(100)
            .WithErrorCode(ApiValidationErrorCodes.TooLong)
            .WithMessage(ApiValidationErrorCodes.TooLong)
            .When(x => x.BadgeNumber is not null);
    }
}
