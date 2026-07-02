using FluentValidation;
using Unified.Calendar.Models;
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
            .LessThan(x => x.EndDate)
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .WithMessage("Start date must be on or before end date.");

        RuleFor(x => x.EndDate)
            .Must((request, endDate) => endDate.DayNumber - request.StartDate.DayNumber + 1 <= MaxRangeLengthDays) // use +1 due to inclusive day range.
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .WithMessage("Date range cannot exceed 366 days.");

        RuleFor(x => x.TimeZoneId)
            .MaximumLength(100)
            .Must(BeValidTimeZoneId)
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .WithMessage("TimeZoneId must be a valid system time zone.")
            .When(x => !string.IsNullOrWhiteSpace(x.TimeZoneId));

        RuleFor(x => x.LocationId)
            .GreaterThanOrEqualTo(0)
            .WithErrorCode(ApiValidationErrorCodes.Invalid)
            .WithMessage("Location ID must be 0 or greater.")
            .When(x => x.LocationId.HasValue);
    }

    private static bool BeValidTimeZoneId(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return true;

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
