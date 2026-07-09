using FluentValidation;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Validators;

public sealed class AssignmentTypeRequestValidator : AbstractValidator<AssignmentTypeRequest>
{
    public AssignmentTypeRequestValidator()
    {
        RuleFor(request => request.Code).NotEmpty().MaximumLength(50);
        RuleFor(request => request.Description).NotEmpty().MaximumLength(100);
        RuleFor(request => request.EffectiveDate).NotEmpty();
        RuleFor(request => request.ExpiryDate)
            .GreaterThanOrEqualTo(request => request.EffectiveDate)
            .When(request => request.ExpiryDate.HasValue);
    }
}
