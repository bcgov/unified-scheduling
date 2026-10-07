using FluentValidation;
using Unified.Common.Time;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Validators;

public sealed class SchedulingCalendarRequestValidator : AbstractValidator<SchedulingCalendarRequest>
{
    public SchedulingCalendarRequestValidator()
    {
        RuleFor(request => request.StartDate).GreaterThan(SchedulingDateRangeRules.MinimumDate);
        RuleFor(request => request.EndDate).GreaterThan(SchedulingDateRangeRules.MinimumDate);
        RuleFor(request => request.StartDate).LessThanOrEqualTo(request => request.EndDate);
        RuleFor(request => request.EndDate)
            .Must((request, endDate) => SchedulingDateRangeRules.IsSupportedRange(request.StartDate, endDate));

        RuleFor(request => request.TimeZoneId)
            .MaximumLength(100)
            .Must(TimeZoneService.IsValidTimeZoneId)
            .WithMessage("TimeZoneId must be a valid system time zone.")
            .When(request => !string.IsNullOrWhiteSpace(request.TimeZoneId));

        RuleFor(request => request.LocationId).GreaterThanOrEqualTo(0).When(request => request.LocationId.HasValue);
        RuleForEach(request => request.UserIds).NotEmpty().When(request => request.UserIds is { Count: > 0 });
    }
}
