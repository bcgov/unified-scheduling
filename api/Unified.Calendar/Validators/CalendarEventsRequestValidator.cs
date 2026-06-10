using FluentValidation;
using Unified.Calendar.Models;
using Unified.Common.Validation;

namespace Unified.Calendar.Validators;

public sealed class CalendarEventsRequestValidator : AbstractValidator<CalendarEventsRequest>
{
    private static readonly DateTimeOffset MinimumDate = new(new DateTime(1900, 1, 1), TimeSpan.Zero);
    private static readonly TimeSpan MaxRangeLength = TimeSpan.FromDays(366);

    public CalendarEventsRequestValidator()
    {
        RuleFor(x => x.StartDate)
            .GreaterThan(MinimumDate)
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .WithMessage(ApiValidationErrorCodes.Invalid);

        RuleFor(x => x.EndDate)
            .GreaterThan(MinimumDate)
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .WithMessage(ApiValidationErrorCodes.Invalid);

        RuleFor(x => x.StartDate)
            .LessThan(x => x.EndDate)
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .WithMessage(ApiValidationErrorCodes.Invalid);

        RuleFor(x => x.EndDate)
            .Must((request, endDate) => endDate - request.StartDate <= MaxRangeLength)
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .WithMessage(ApiValidationErrorCodes.Invalid);

        RuleFor(x => x.LocationId)
            .GreaterThanOrEqualTo(0)
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .WithMessage(ApiValidationErrorCodes.Invalid)
            .When(x => x.LocationId.HasValue);
    }
}