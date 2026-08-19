using FluentValidation;
using Unified.Calendar.Models;
using Unified.Common.Helpers.Extensions;
using Unified.Common.Time;
using Unified.Common.Validation;

namespace Unified.Calendar.Validators;

public sealed class CalendarDataRequestValidator : AbstractValidator<CalendarDataRequest>
{
    private static readonly DateOnly MinimumDate = new(1900, 1, 1);
    private const int MaxRangeLengthDays = 366;

    public CalendarDataRequestValidator()
    {
        RuleFor(x => x.StartDate)
            .GreaterThan(MinimumDate)
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .WithMessage("Start date must be after 1900-01-01.");

        RuleFor(x => x.EndDate)
            .GreaterThan(MinimumDate)
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .WithMessage("End date must be after 1900-01-01.");

        RuleFor(x => x.StartDate)
            .LessThanOrEqualTo(TimeZoneDateRangeLimits.MaximumSupportedDate)
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .WithMessage($"Start date cannot be after {TimeZoneDateRangeLimits.MaximumSupportedDate:yyyy-MM-dd}.");

        RuleFor(x => x.EndDate)
            .LessThanOrEqualTo(TimeZoneDateRangeLimits.MaximumSupportedDate)
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .WithMessage($"End date cannot be after {TimeZoneDateRangeLimits.MaximumSupportedDate:yyyy-MM-dd}.");

        RuleFor(x => x.StartDate)
            .LessThanOrEqualTo(x => x.EndDate)
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .WithMessage("Start date must be on or before end date.");

        RuleFor(x => x.EndDate)
            .Must((request, endDate) => endDate.DayNumber - request.StartDate.DayNumber + 1 <= MaxRangeLengthDays)
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .WithMessage("Date range cannot exceed 366 days.");

        RuleFor(x => x.TimeZoneId)
            .MaximumLength(100)
            .Must(TimeZoneService.IsValidTimeZoneId)
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .WithMessage("TimeZoneId must be a valid system time zone.")
            .When(x => !string.IsNullOrWhiteSpace(x.TimeZoneId));

        RuleFor(x => x.LocationId)
            .GreaterThanOrEqualTo(0)
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .WithMessage("Location ID must be 0 or greater.")
            .When(x => x.LocationId.HasValue);
    }
}
