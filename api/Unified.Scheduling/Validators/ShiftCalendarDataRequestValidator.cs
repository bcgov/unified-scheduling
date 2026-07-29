using FluentValidation;
using Unified.Common.Time;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Validators;

public sealed class SchedulingCalendarRequestValidator : AbstractValidator<SchedulingCalendarRequest>
{
    private static readonly DateOnly MinimumDate = new(1900, 1, 1);
    private const int MaxRangeLengthDays = 366;

    public SchedulingCalendarRequestValidator()
    {
        RuleFor(request => request.StartDate).GreaterThan(MinimumDate);
        RuleFor(request => request.EndDate).GreaterThan(MinimumDate);
        RuleFor(request => request.StartDate).LessThanOrEqualTo(request => request.EndDate);
        RuleFor(request => request.EndDate)
            .Must((request, endDate) => endDate.DayNumber - request.StartDate.DayNumber + 1 <= MaxRangeLengthDays);

        RuleFor(request => request.TimeZoneId)
            .MaximumLength(100)
            .Must(TimeZoneService.IsValidTimeZoneId)
            .WithMessage("TimeZoneId must be a valid system time zone.")
            .When(request => !string.IsNullOrWhiteSpace(request.TimeZoneId));

        RuleFor(request => request.LocationId).GreaterThanOrEqualTo(0).When(request => request.LocationId.HasValue);
        RuleForEach(request => request.UserIds).NotEmpty().When(request => request.UserIds is { Count: > 0 });
    }
}
