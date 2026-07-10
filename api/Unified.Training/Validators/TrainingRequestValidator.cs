using FluentValidation;
using Unified.Common.Validation;
using Unified.Training.Models;

namespace Unified.Training.Validators;

public sealed class TrainingRequestValidator : AbstractValidator<TrainingRequest>
{
    public TrainingRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .WithErrorCode(ApiValidationErrorCodes.Required)
            .WithMessage(ApiValidationErrorCodes.Required)
            .MaximumLength(50)
            .WithErrorCode(ApiValidationErrorCodes.TooLong)
            .WithMessage(ApiValidationErrorCodes.TooLong);

        RuleFor(x => x.Description)
            .NotEmpty()
            .WithErrorCode(ApiValidationErrorCodes.Required)
            .WithMessage(ApiValidationErrorCodes.Required)
            .MaximumLength(200)
            .WithErrorCode(ApiValidationErrorCodes.TooLong)
            .WithMessage(ApiValidationErrorCodes.TooLong);

        RuleFor(x => x.TrainingCategoryId)
            .GreaterThan(0)
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .WithMessage(ApiValidationErrorCodes.Invalid)
            .When(x => x.TrainingCategoryId.HasValue);

        RuleFor(x => x.ValidityDays)
            .GreaterThan(0)
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .WithMessage(ApiValidationErrorCodes.Invalid)
            .When(x => x.ValidityDays.HasValue);

        RuleFor(x => x.AdvanceNoticeDays)
            .GreaterThanOrEqualTo(0)
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .WithMessage(ApiValidationErrorCodes.Invalid)
            .When(x => x.AdvanceNoticeDays.HasValue);

        RuleFor(x => x.AdvanceNoticeDays)
            .LessThan(x => x.ValidityDays)
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .WithMessage(ApiValidationErrorCodes.Invalid)
            .When(x => x.AdvanceNoticeDays.HasValue && x.ValidityDays.HasValue);

        RuleFor(x => x.Order)
            .GreaterThanOrEqualTo(0)
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .WithMessage(ApiValidationErrorCodes.Invalid);
    }
}
