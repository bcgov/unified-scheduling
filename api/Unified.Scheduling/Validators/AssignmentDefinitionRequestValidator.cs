using FluentValidation;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Validators;

public sealed class AssignmentDefinitionRequestValidator : AbstractValidator<AssignmentDefinitionRequest>
{
    public AssignmentDefinitionRequestValidator()
    {
        RuleFor(request => request.LocationId).GreaterThan(0);
        RuleFor(request => request.Name).NotEmpty().MaximumLength(50);
        RuleFor(request => request.Description).MaximumLength(200);
        RuleFor(request => request.AssignmentCategoryTypeId).GreaterThan(0);
        RuleFor(request => request.AssignmentSubCategoryTypeId).GreaterThan(0);
        RuleFor(request => request.Color).MaximumLength(100);
        RuleFor(request => request.DefaultCapacity).GreaterThanOrEqualTo(1);
        RuleFor(request => request.EffectiveDateUtc).NotEmpty();
        RuleFor(request => request.ExpiryDateUtc)
            .GreaterThan(request => request.EffectiveDateUtc)
            .When(request => request.ExpiryDateUtc.HasValue);
        RuleFor(request => request.DefaultStartTime)
            .Must(value => string.IsNullOrWhiteSpace(value) || TimeOnly.TryParse(value, out _))
            .WithMessage("Default start time must be a valid time.");
        RuleFor(request => request.DefaultEndTime)
            .Must(value => string.IsNullOrWhiteSpace(value) || TimeOnly.TryParse(value, out _))
            .WithMessage("Default end time must be a valid time.");
        RuleFor(request => request)
            .Must(request =>
                string.IsNullOrWhiteSpace(request.DefaultStartTime)
                || string.IsNullOrWhiteSpace(request.DefaultEndTime)
                || TimeOnly.Parse(request.DefaultEndTime) > TimeOnly.Parse(request.DefaultStartTime))
            .WithMessage("Default end time must be after default start time.");
    }
}
