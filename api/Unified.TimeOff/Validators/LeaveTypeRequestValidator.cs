using FluentValidation;
using Unified.TimeOff.Models;

namespace Unified.TimeOff.Validators;

public sealed class LeaveTypeRequestValidator : AbstractValidator<LeaveTypeRequest>
{
    public LeaveTypeRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(50);
        RuleFor(request => request.Description).NotEmpty().MaximumLength(100);
        RuleFor(request => request.EffectiveDateUtc)
            .LessThan(request => request.ExpiryDateUtc!.Value)
            .When(request => request.EffectiveDateUtc.HasValue && request.ExpiryDateUtc.HasValue)
            .WithMessage("Effective date must be before the expiry date.");
    }
}
