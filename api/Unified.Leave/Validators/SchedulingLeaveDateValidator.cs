using FluentValidation;
using Unified.Common.Time;
using Unified.Leave.Models.Scheduling;

namespace Unified.Leave.Validators;

public sealed class SchedulingLeaveDateValidator<TRequest> : AbstractValidator<TRequest>
    where TRequest : SchedulingLeaveRequest
{
    public SchedulingLeaveDateValidator()
    {
        When(
            request => request.AllDay,
            () =>
            {
                RuleFor(request => request.LeaveDate).NotNull();
                RuleFor(request => request.LeaveDate)
                    .Must(date =>
                        !date.HasValue
                        || (
                            date.Value > DateOnly.MinValue && date.Value <= TimeZoneDateRangeLimits.MaximumSupportedDate
                        )
                    )
                    .WithMessage("Leave date must be a supported calendar date.");
                RuleFor(request => request.StartAtUtc)
                    .Null()
                    .WithMessage("All-day leave must not include a start time.");
                RuleFor(request => request.EndAtUtc).Null().WithMessage("All-day leave must not include an end time.");
                RuleFor(request => request.LocationId).NotNull().GreaterThan(0);
            }
        );
        When(
            request => !request.AllDay,
            () =>
            {
                RuleFor(request => request.LeaveDate).Null().WithMessage("Timed leave must not include a leave date.");
                RuleFor(request => request.StartAtUtc).NotNull().NotEqual(default(DateTimeOffset));
                RuleFor(request => request.EndAtUtc).NotNull();
                RuleFor(request => request.StartAtUtc)
                    .Must(
                        (request, start) =>
                            !start.HasValue || !request.EndAtUtc.HasValue || start.Value < request.EndAtUtc.Value
                    )
                    .WithMessage("End time must be after start time.");
            }
        );
    }
}
