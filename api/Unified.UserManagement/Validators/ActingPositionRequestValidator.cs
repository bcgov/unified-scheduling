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

        RuleFor(x => x.StartDate).NotEmpty().WithMessage("StartDate is required.");

        RuleFor(x => x.StartDate)
            .Must(x => DateTimeOffsetExtensions.IsValidDateFormat(x, DateTimeOffsetExtensions.DateFormat))
            .When(x => !string.IsNullOrEmpty(x.StartDate))
            .WithMessage($"StartDate must be in {DateTimeOffsetExtensions.DateFormat} format.");

        RuleFor(x => x.EndDate).NotEmpty().WithMessage("EndDate is required.");

        RuleFor(x => x.EndDate)
            .Must(x => DateTimeOffsetExtensions.IsValidDateFormat(x, DateTimeOffsetExtensions.DateFormat))
            .When(x => !string.IsNullOrEmpty(x.EndDate))
            .WithMessage($"EndDate must be in {DateTimeOffsetExtensions.DateFormat} format.");

        RuleFor(x => x)
            .Must(x =>
            {
                if (
                    string.IsNullOrEmpty(x.StartDate)
                    || string.IsNullOrEmpty(x.EndDate)
                    || !DateTimeOffsetExtensions.IsValidDateFormat(x.StartDate, DateTimeOffsetExtensions.DateFormat)
                    || !DateTimeOffsetExtensions.IsValidDateFormat(x.EndDate, DateTimeOffsetExtensions.DateFormat)
                )
                {
                    return true;
                }

                return DateOnly.Parse(x.EndDate) >= DateOnly.Parse(x.StartDate);
            })
            .WithMessage("EndDate must be on or after StartDate.");

        RuleFor(x => x.Comment)
            .MaximumLength(500)
            .WithMessage("Comment must not exceed 500 characters.")
            .When(x => x.Comment is not null);
    }
}
