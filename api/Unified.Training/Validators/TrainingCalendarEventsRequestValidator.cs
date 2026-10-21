using FluentValidation;
using Unified.Common.Time;
using Unified.Common.Validation;
using Unified.Training.Models.Calendar;

namespace Unified.Training.Validators;

public sealed class TrainingCalendarEventsRequestValidator : AbstractValidator<TrainingCalendarEventsRequest>
{
    private static readonly DateOnly MinimumDate = new(1900, 1, 1);
    private const int MaxRangeLengthDays = 366;

    public TrainingCalendarEventsRequestValidator()
    {
        RuleFor(x => x.StartDate).GreaterThan(MinimumDate).WithErrorCode(ApiValidationErrorCodes.Invalid);
        RuleFor(x => x.StartDate)
            .LessThanOrEqualTo(TimeZoneDateRangeLimits.MaximumSupportedDate)
            .WithErrorCode(ApiValidationErrorCodes.Invalid);
        RuleFor(x => x.EndDate).GreaterThan(MinimumDate).WithErrorCode(ApiValidationErrorCodes.Invalid);
        RuleFor(x => x.EndDate)
            .LessThanOrEqualTo(TimeZoneDateRangeLimits.MaximumSupportedDate)
            .WithErrorCode(ApiValidationErrorCodes.Invalid);
        RuleFor(x => x.StartDate).LessThanOrEqualTo(x => x.EndDate).WithErrorCode(ApiValidationErrorCodes.Invalid);
        RuleFor(x => x.EndDate)
            .Must((request, endDate) => endDate.DayNumber - request.StartDate.DayNumber + 1 <= MaxRangeLengthDays)
            .WithErrorCode(ApiValidationErrorCodes.Invalid);
        RuleFor(x => x.LocationId)
            .GreaterThanOrEqualTo(0)
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .When(x => x.LocationId.HasValue);
        RuleForEach(x => x.UserIds).NotEqual(Guid.Empty).WithErrorCode(ApiValidationErrorCodes.Invalid);
    }
}
