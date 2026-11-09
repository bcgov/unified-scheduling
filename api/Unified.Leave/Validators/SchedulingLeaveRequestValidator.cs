using FluentValidation;
using Unified.Common.Time;
using Unified.Leave.Models.Scheduling;

namespace Unified.Leave.Validators;

public sealed class SchedulingLeaveRequestValidator : AbstractValidator<SchedulingLeaveEntryRequest>
{
    public SchedulingLeaveRequestValidator()
    {
        Include(new SchedulingLeaveDateValidator<SchedulingLeaveEntryRequest>());
        RuleFor(request => request.LeaveSeriesId).GreaterThan(0).When(request => request.LeaveSeriesId.HasValue);
        RuleFor(request => request.LeaveTypeId).NotEmpty().GreaterThan(0);
        RuleFor(request => request.Title).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Description).MaximumLength(2000);
        RuleFor(request => request.Notes).MaximumLength(4000);
        RuleFor(request => request.Color).MaximumLength(100);
        RuleFor(request => request.TimeZoneId).MaximumLength(100).Must(TimeZoneService.IsValidTimeZoneId);
        RuleFor(request => request.LocationId).GreaterThan(0);
        RuleFor(request => request.UserIds)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(userIds => userIds.Distinct().Count() == userIds.Count)
            .WithMessage("Users must be unique.");
        RuleForEach(request => request.UserIds).NotEmpty();
    }
}
