using FluentValidation;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Validators;

public sealed class AssignmentSeriesRequestValidator : AbstractValidator<AssignmentSeriesRequest>
{
    public AssignmentSeriesRequestValidator()
    {
        RuleFor(request => request.Title).NotEmpty().MaximumLength(200);
        RuleFor(request => request.EndAtUtc).GreaterThan(request => request.StartAtUtc).When(request => request.EndAtUtc.HasValue);
        RuleFor(request => request.AssignmentCategoryTypeId).GreaterThan(0);
        RuleFor(request => request.AssignmentSubCategoryTypeId).GreaterThan(0);
        RuleFor(request => request.AssignmentTypeId).GreaterThan(0);
        RuleFor(request => request.Capacity).GreaterThanOrEqualTo(1);
        RuleFor(request => request.RecurrenceRule).NotEmpty();
    }
}

public sealed class AssignmentEntryRequestValidator : AbstractValidator<AssignmentEntryRequest>
{
    public AssignmentEntryRequestValidator()
    {
        RuleFor(request => request.AssignmentSeriesId).GreaterThan(0).When(request => request.AssignmentSeriesId.HasValue);
        RuleFor(request => request.Title).NotEmpty().MaximumLength(200);
        RuleFor(request => request.EndAtUtc).GreaterThan(request => request.StartAtUtc).When(request => request.EndAtUtc.HasValue);
        RuleFor(request => request.AssignmentCategoryTypeId).GreaterThan(0);
        RuleFor(request => request.AssignmentSubCategoryTypeId).GreaterThan(0);
        RuleFor(request => request.AssignmentTypeId).GreaterThan(0);
        RuleFor(request => request.Capacity).GreaterThanOrEqualTo(1);
    }
}
