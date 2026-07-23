using FluentValidation;
using Unified.Common.Helpers.Extensions;
using Unified.UserManagement.Models;

namespace Unified.UserManagement.Validators;

public class AwayLocationRequestValidator : AbstractValidator<AwayLocationRequestDto>
{
    public AwayLocationRequestValidator()
    {
        RuleFor(x => x.LocationId).GreaterThan(0).WithMessage("LocationId is required.");

        RuleFor(x => x.StartDateTime).NotEmpty().WithMessage("StartDateTime is required.");

        RuleFor(x => x.StartDateTime)
            .Must(x => DateTimeOffsetExtensions.IsValidIsoDateTimeWithOffset(x))
            .When(x => !string.IsNullOrEmpty(x.StartDateTime))
            .WithMessage("StartDateTime must be a valid ISO 8601 datetime with UTC offset.");

        RuleFor(x => x.EndDateTime).NotEmpty().WithMessage("EndDateTime is required.");

        RuleFor(x => x.EndDateTime)
            .Must(x => DateTimeOffsetExtensions.IsValidIsoDateTimeWithOffset(x))
            .When(x => !string.IsNullOrEmpty(x.EndDateTime))
            .WithMessage("EndDateTime must be a valid ISO 8601 datetime with UTC offset.");

        RuleFor(x => x)
            .Must(x =>
            {
                if (
                    string.IsNullOrEmpty(x.StartDateTime)
                    || string.IsNullOrEmpty(x.EndDateTime)
                    || !DateTimeOffsetExtensions.IsValidIsoDateTimeWithOffset(x.StartDateTime)
                    || !DateTimeOffsetExtensions.IsValidIsoDateTimeWithOffset(x.EndDateTime)
                    || !DateTimeOffset.TryParse(x.StartDateTime, out var start)
                    || !DateTimeOffset.TryParse(x.EndDateTime, out var end)
                )
                {
                    return true;
                }

                return end > start;
            })
            .WithMessage("EndDateTime must be after StartDateTime.");

        RuleFor(x => x.Timezone)
            .Must(tz => DateTimeOffsetExtensions.IsValidIanaTimezone(tz))
            .When(x => x.Timezone is not null)
            .WithMessage("Timezone must be a valid IANA timezone identifier.");

        RuleFor(x => x.Comment)
            .MaximumLength(500)
            .WithMessage("Comment must not exceed 500 characters.")
            .When(x => x.Comment is not null);
    }
}
