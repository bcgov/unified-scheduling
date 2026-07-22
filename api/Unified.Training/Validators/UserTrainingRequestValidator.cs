using FluentValidation;
using Unified.Common.Validation;
using Unified.Training.Models;

namespace Unified.Training.Validators;

public sealed class UserTrainingRequestValidator : AbstractValidator<UserTrainingRequest>
{
    public UserTrainingRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithErrorCode(ApiValidationErrorCodes.Required)
            .WithMessage(ApiValidationErrorCodes.Required);

        RuleFor(x => x.TrainingId)
            .GreaterThan(0)
            .WithErrorCode(ApiValidationErrorCodes.Required)
            .WithMessage(ApiValidationErrorCodes.Required);

        RuleFor(x => x.AwardedOn)
            .NotEmpty()
            .WithErrorCode(ApiValidationErrorCodes.Required)
            .WithMessage(ApiValidationErrorCodes.Required);

        RuleFor(x => x.ExpiryDate)
            .GreaterThan(x => x.AwardedOn)
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .WithMessage("Expiry date must be after the awarded date.")
            .When(x => x.ExpiryDate.HasValue);

        RuleFor(x => x.Notes)
            .MaximumLength(2000)
            .WithErrorCode(ApiValidationErrorCodes.TooLong)
            .WithMessage(ApiValidationErrorCodes.TooLong)
            .When(x => x.Notes is not null);
    }
}
