using FluentValidation;
using Unified.Common.Time;
using Unified.TimeOff.Models.Scheduling;

namespace Unified.TimeOff.Validators;

public sealed class SchedulingTimeOffRequestValidator : AbstractValidator<SchedulingTimeOffEntryRequest>
{
    public SchedulingTimeOffRequestValidator()
    {
        RuleFor(request => request.LeaveTypeId).NotEmpty().GreaterThan(0);
        RuleFor(request => request.Title).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Description).MaximumLength(2000);
        RuleFor(request => request.Notes).MaximumLength(4000);
        RuleFor(request => request.Color).MaximumLength(100);
        RuleFor(request => request.TimeZoneId).MaximumLength(100).Must(TimeZoneService.IsValidTimeZoneId);
        RuleFor(request => request.StartAtUtc)
            .LessThan(request => request.EndAtUtc!.Value)
            .When(request => request.EndAtUtc.HasValue);
        RuleFor(request => request.SeriesStartAtUtc)
            .LessThan(request => request.SeriesEndAtUtc!.Value)
            .When(request => request.SeriesStartAtUtc.HasValue && request.SeriesEndAtUtc.HasValue);
        RuleFor(request => request.LocationId).GreaterThan(0);
        RuleFor(request => request.UserIds)
            .NotEmpty()
            .Must(userIds => userIds.Distinct().Count() == userIds.Count)
            .WithMessage("Users must be unique.");
        RuleForEach(request => request.UserIds).NotEmpty();
    }
}
