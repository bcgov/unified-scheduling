using FluentValidation;
using Unified.Common.Validation;
using Unified.Stats.Models;

namespace Unified.Stats.Validators;

public class StatRecordRequestValidator : AbstractValidator<StatRecordRequest>
{
    public StatRecordRequestValidator()
    {
        RuleFor(x => x.DateFrom)
            .NotEmpty()
            .WithErrorCode(ApiValidationErrorCodes.Required)
            .WithMessage(ApiValidationErrorCodes.Required);

        RuleFor(x => x.DateTo)
            .NotEmpty()
            .WithErrorCode(ApiValidationErrorCodes.Required)
            .WithMessage(ApiValidationErrorCodes.Required)
            .GreaterThanOrEqualTo(x => x.DateFrom)
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .WithMessage(ApiValidationErrorCodes.Invalid);

        RuleFor(x => x.PeriodType)
            .NotEmpty()
            .WithErrorCode(ApiValidationErrorCodes.Required)
            .WithMessage(ApiValidationErrorCodes.Required)
            .MaximumLength(50)
            .WithErrorCode(ApiValidationErrorCodes.TooLong)
            .WithMessage(ApiValidationErrorCodes.TooLong);

        RuleFor(x => x.LocationId)
            .GreaterThan(0)
            .WithErrorCode(ApiValidationErrorCodes.Required)
            .WithMessage(ApiValidationErrorCodes.Required);

        RuleFor(x => x.SubCategoryMetricId)
            .GreaterThan(0)
            .WithErrorCode(ApiValidationErrorCodes.Required)
            .WithMessage(ApiValidationErrorCodes.Required);

        RuleFor(x => x.Comment)
            .MaximumLength(1000)
            .WithErrorCode(ApiValidationErrorCodes.TooLong)
            .WithMessage(ApiValidationErrorCodes.TooLong)
            .When(x => x.Comment is not null);
    }
}
