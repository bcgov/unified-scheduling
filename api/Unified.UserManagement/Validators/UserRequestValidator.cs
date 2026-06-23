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
            .WithMessage("IDIR name is required.")
            .MaximumLength(200)
            .WithErrorCode(ApiValidationErrorCodes.TooLong)
            .WithMessage("IDIR name must be 200 characters or less.");

        RuleFor(x => x.FirstName)
            .NotEmpty()
            .WithErrorCode(ApiValidationErrorCodes.Required)
            .WithMessage("First name is required.")
            .MaximumLength(150)
            .WithErrorCode(ApiValidationErrorCodes.TooLong)
            .WithMessage("First name must be 150 characters or less.");

        RuleFor(x => x.LastName)
            .NotEmpty()
            .WithErrorCode(ApiValidationErrorCodes.Required)
            .WithMessage("Last name is required.")
            .MaximumLength(150)
            .WithErrorCode(ApiValidationErrorCodes.TooLong)
            .WithMessage("Last name must be 150 characters or less.");

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithErrorCode(ApiValidationErrorCodes.Required)
            .WithMessage("Email is required.")
            .MaximumLength(320)
            .WithErrorCode(ApiValidationErrorCodes.TooLong)
            .WithMessage("Email must be 320 characters or less.")
            .EmailAddress()
            .WithErrorCode(ApiValidationErrorCodes.InvalidEmail)
            .WithMessage("Email must be a valid email address.")
            .When(x => x.Email is not null);

        RuleFor(x => x.Gender)
            .IsInEnum()
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .WithMessage("Gender must be a valid value.");

        RuleFor(x => x.HomeLocationId)
            .GreaterThan(0)
            .WithErrorCode(ApiValidationErrorCodes.Required)
            .WithMessage("Home location is required.");

        RuleFor(x => x.Rank)
            .NotEmpty()
            .WithErrorCode(ApiValidationErrorCodes.Required)
            .WithMessage("Rank is required.")
            .MaximumLength(150)
            .WithErrorCode(ApiValidationErrorCodes.TooLong)
            .WithMessage("Rank must be 150 characters or less.");

        RuleFor(x => x.BadgeNumber)
            .MaximumLength(100)
            .WithErrorCode(ApiValidationErrorCodes.TooLong)
            .WithMessage("Badge number must be 100 characters or less.")
            .When(x => x.BadgeNumber is not null);
    }
}
