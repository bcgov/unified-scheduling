using FluentValidation;
using Unified.Common.Time;
using Unified.LoanOut.Models.Calendar;

namespace Unified.LoanOut.Validators;

public sealed class LoanOutUserSearchRequestValidator : AbstractValidator<LoanOutUserSearchRequest>
{
    public LoanOutUserSearchRequestValidator()
    {
        RuleFor(request => request.StartDate)
            .NotEmpty()
            .WithMessage("StartDate is required.")
            .GreaterThan(LoanOutDateRangeRules.MinimumDate);
        RuleFor(request => request.EndDate)
            .NotEmpty()
            .WithMessage("EndDate is required.")
            .GreaterThan(LoanOutDateRangeRules.MinimumDate);
        RuleFor(request => request.StartDate).LessThanOrEqualTo(request => request.EndDate);
        RuleFor(request => request.EndDate)
            .NotEmpty()
            .WithMessage("EndDate is required.")
            .Must((request, endDate) => LoanOutDateRangeRules.IsSupportedRange(request.StartDate, endDate))
            .WithMessage("The date range is not supported.");

        RuleFor(request => request.TimeZoneId)
            .MaximumLength(100)
            .Must(TimeZoneService.IsValidTimeZoneId)
            .WithMessage("TimeZoneId must be a valid system time zone.")
            .When(request => !string.IsNullOrWhiteSpace(request.TimeZoneId));

        RuleFor(request => request.LocationId).GreaterThanOrEqualTo(0).When(request => request.LocationId.HasValue);
    }
}

internal static class LoanOutDateRangeRules
{
    public static readonly DateOnly MinimumDate = new(1900, 1, 1);
    public const int MaxRangeLengthDays = 366;

    public static bool IsSupportedRange(DateOnly startDate, DateOnly endDate) =>
        endDate.DayNumber - startDate.DayNumber + 1 <= MaxRangeLengthDays;
}
