using FluentValidation;
using Unified.Leave.Services;

namespace Unified.Leave.Validators;

public sealed class LeaveCalendarQueryValidator : AbstractValidator<CalendarEventQueryContext>
{
    public LeaveCalendarQueryValidator()
    {
        RuleFor(request => request.StartAtUtc).NotEmpty();
        RuleFor(request => request.EndAtUtc)
            .GreaterThan(request => request.StartAtUtc)
            .Must((request, end) => end - request.StartAtUtc <= TimeSpan.FromDays(367))
            .WithMessage("The date range must not exceed 367 days.");
        RuleFor(request => request.UserIds).NotEmpty();
        RuleForEach(request => request.UserIds).NotEmpty();
        RuleFor(request => request.LocationId).GreaterThan(0).When(request => request.LocationId.HasValue);
    }
}
