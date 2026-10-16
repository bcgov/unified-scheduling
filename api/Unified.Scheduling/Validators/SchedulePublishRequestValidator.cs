using FluentValidation;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Validators;

public sealed class SchedulePublishRequestValidator : AbstractValidator<SchedulePublishRequest>
{
    public SchedulePublishRequestValidator()
    {
        RuleFor(request => request.LocationId).GreaterThan(0);
        RuleFor(request => request.StartDate).GreaterThan(SchedulingDateRangeRules.MinimumDate);
        RuleFor(request => request.EndDate).GreaterThan(SchedulingDateRangeRules.MinimumDate);
        RuleFor(request => request.StartDate).LessThanOrEqualTo(request => request.EndDate);
        RuleFor(request => request.EndDate)
            .Must((request, endDate) => SchedulingDateRangeRules.IsSupportedRange(request.StartDate, endDate));
    }
}
