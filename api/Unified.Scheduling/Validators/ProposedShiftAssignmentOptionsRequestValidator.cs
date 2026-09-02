using FluentValidation;
using Unified.Common.Time;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Validators;

public sealed class ProposedShiftAssignmentOptionsRequestValidator
    : AbstractValidator<ProposedShiftAssignmentOptionsRequest>
{
    public ProposedShiftAssignmentOptionsRequestValidator()
    {
        RuleFor(request => request.LocationId).GreaterThan(0);
        RuleFor(request => request.StartAtUtc).NotEqual(default(DateTimeOffset));
        RuleFor(request => request.EndAtUtc).GreaterThan(request => request.StartAtUtc);
        RuleFor(request => request.TimeZoneId)
            .NotEmpty()
            .MaximumLength(100)
            .Must(TimeZoneService.IsValidTimeZoneId)
            .WithMessage("TimeZoneId must be a valid system time zone.");
    }
}