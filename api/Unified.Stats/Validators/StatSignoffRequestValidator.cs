using FluentValidation;
using Unified.Common.Validation;
using Unified.Stats.Models;

namespace Unified.Stats.Validators;

public class StatSignoffRequestValidator : AbstractValidator<StatSignoffRequest>
{
    public StatSignoffRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithErrorCode(ApiValidationErrorCodes.Required)
            .WithMessage(ApiValidationErrorCodes.Required);

        RuleFor(x => x.LocationId)
            .GreaterThan(0)
            .WithErrorCode(ApiValidationErrorCodes.Required)
            .WithMessage(ApiValidationErrorCodes.Required);

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12)
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .WithMessage(ApiValidationErrorCodes.Invalid);

        RuleFor(x => x.Year)
            .GreaterThan(2000)
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .WithMessage(ApiValidationErrorCodes.Invalid);
    }
}
