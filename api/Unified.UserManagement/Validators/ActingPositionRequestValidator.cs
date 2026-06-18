using FluentValidation;
using Unified.Common.Helpers.Extensions;
using Unified.UserManagement.Models;

namespace Unified.UserManagement.Validators;

public class ActingPositionRequestValidator : AbstractValidator<ActingPositionRequestDto>
{
    public ActingPositionRequestValidator()
    {
        RuleFor(x => x.PositionTypeCode)
            .NotEmpty()
            .WithMessage("PositionTypeCode is required.")
            .MaximumLength(50)
            .WithMessage("PositionTypeCode must not exceed 50 characters.");

        RuleFor(x => x.EffectiveDate)
            .NotEmpty()
            .WithMessage("EffectiveDate is required.");

        RuleFor(x => x.EffectiveDate)
            .Must(x => DateTimeOffsetExtensions.IsValidDateFormat(x, DateTimeOffsetExtensions.DateFormat))
            .When(x => !string.IsNullOrEmpty(x.EffectiveDate))
            .WithMessage($"EffectiveDate must be in {DateTimeOffsetExtensions.DateFormat} format.");

        RuleFor(x => x.ExpiryDate)
            .Must(x => DateTimeOffsetExtensions.IsValidDateFormat(x, DateTimeOffsetExtensions.DateFormat))
            .When(x => !string.IsNullOrEmpty(x.ExpiryDate))
            .WithMessage($"ExpiryDate must be in {DateTimeOffsetExtensions.DateFormat} format.");

        RuleFor(x => x)
            .Must(x =>
                string.IsNullOrEmpty(x.ExpiryDate)
                || DateOnly.ParseExact(x.ExpiryDate, DateTimeOffsetExtensions.DateFormat)
                    >= DateOnly.ParseExact(x.EffectiveDate, DateTimeOffsetExtensions.DateFormat)
            )
            .When(x =>
                !string.IsNullOrEmpty(x.EffectiveDate)
                && !string.IsNullOrEmpty(x.ExpiryDate)
                && DateTimeOffsetExtensions.IsValidDateFormat(x.EffectiveDate)
                && DateTimeOffsetExtensions.IsValidDateFormat(x.ExpiryDate)
            )
            .WithMessage("ExpiryDate must be on or after EffectiveDate.");

        RuleFor(x => x.Comment)
            .MaximumLength(500)
            .WithMessage("Comment must not exceed 500 characters.")
            .When(x => x.Comment is not null);
    }
}
