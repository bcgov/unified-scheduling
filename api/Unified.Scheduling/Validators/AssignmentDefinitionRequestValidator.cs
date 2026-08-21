using FluentValidation;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Validators;

public sealed class AssignmentDefinitionRequestValidator : AbstractValidator<AssignmentDefinitionRequest>
{
    public AssignmentDefinitionRequestValidator()
    {
        RuleFor(request => request.LocationId).GreaterThan(0);
        RuleFor(request => request.Name).NotEmpty().MaximumLength(100);
        RuleFor(request => request.Description).MaximumLength(500);
        RuleFor(request => request.CategoryId).GreaterThan(0);
        RuleFor(request => request.SubCategoryId).GreaterThan(0);
        RuleFor(request => request.Color).MaximumLength(100);
        RuleFor(request => request.DefaultCapacity).GreaterThanOrEqualTo(1);
        RuleFor(request => request.EffectiveDateUtc).NotEmpty();
        RuleFor(request => request.ExpiryDateUtc)
            .Must(
                (request, expiryDateUtc) =>
                    !expiryDateUtc.HasValue
                    || GetUtcBusinessDate(expiryDateUtc.Value) > GetUtcBusinessDate(request.EffectiveDateUtc)
            )
            .WithMessage("Expiry date must be after effective date.")
            .When(request => request.ExpiryDateUtc.HasValue);
        RuleFor(request => request.DefaultStartTime)
            .Must(value => string.IsNullOrWhiteSpace(value) || TimeOnly.TryParse(value, out _))
            .WithMessage("Default start time must be a valid time.");
        RuleFor(request => request.DefaultEndTime)
            .Must(value => string.IsNullOrWhiteSpace(value) || TimeOnly.TryParse(value, out _))
            .WithMessage("Default end time must be a valid time.");
        RuleFor(request => request)
            .Must(HaveValidDefaultTimeOrder)
            .WithMessage("Default end time must be after default start time.");
    }

    private static bool HaveValidDefaultTimeOrder(AssignmentDefinitionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DefaultStartTime) || string.IsNullOrWhiteSpace(request.DefaultEndTime))
            return true;

        return !TimeOnly.TryParse(request.DefaultStartTime, out var start)
            || !TimeOnly.TryParse(request.DefaultEndTime, out var end)
            || end > start;
    }

    private static DateOnly GetUtcBusinessDate(DateTimeOffset value) => DateOnly.FromDateTime(value.UtcDateTime);
}
