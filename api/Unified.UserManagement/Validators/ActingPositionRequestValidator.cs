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

        RuleFor(x => x.StartDateTime).NotEmpty().WithMessage("StartDateTime is required.");

        RuleFor(x => x.StartDateTime)
            .Must(x => DateTimeOffsetExtensions.IsValidLocalDateTimeFormat(x))
            .When(x => !string.IsNullOrEmpty(x.StartDateTime))
            .WithMessage($"StartDateTime must be in {DateTimeOffsetExtensions.LocalDateTimeFormat} format.");

        RuleFor(x => x.EndDateTime).NotEmpty().WithMessage("EndDateTime is required.");

        RuleFor(x => x.EndDateTime)
            .Must(x => DateTimeOffsetExtensions.IsValidLocalDateTimeFormat(x))
            .When(x => !string.IsNullOrEmpty(x.EndDateTime))
            .WithMessage($"EndDateTime must be in {DateTimeOffsetExtensions.LocalDateTimeFormat} format.");

        RuleFor(x => x)
            .Must(x =>
            {
                if (
                    string.IsNullOrEmpty(x.StartDateTime)
                    || string.IsNullOrEmpty(x.EndDateTime)
                    || !DateTimeOffsetExtensions.IsValidLocalDateTimeFormat(x.StartDateTime)
                    || !DateTimeOffsetExtensions.IsValidLocalDateTimeFormat(x.EndDateTime)
                )
                {
                    return true;
                }

                // ISO-format strings are lexicographically comparable
                return string.Compare(x.EndDateTime, x.StartDateTime, StringComparison.Ordinal) > 0;
            })
            .WithMessage("EndDateTime must be after StartDateTime.");

        RuleFor(x => x.Comment)
            .MaximumLength(500)
            .WithMessage("Comment must not exceed 500 characters.")
            .When(x => x.Comment is not null);
    }
}
