using FluentValidation;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Validators;

public sealed class ShiftAssignmentEntryRequestValidator : AbstractValidator<ShiftAssignmentEntryRequest>
{
    public ShiftAssignmentEntryRequestValidator()
    {
        RuleFor(request => request.ShiftEntryId).GreaterThan(0);
        RuleFor(request => request.AssignmentEntryId).GreaterThan(0);
        RuleFor(request => request.UserIds).NotEmpty();
        RuleFor(request => request.UserIds).Must(HaveUniqueUsers).WithMessage("Selected users must be unique.");
    }

    private static bool HaveUniqueUsers(IReadOnlyCollection<Guid> userIds) => userIds.Distinct().Count() == userIds.Count;
}

public sealed class ShiftAssignmentSeriesRequestValidator : AbstractValidator<ShiftAssignmentSeriesRequest>
{
    public ShiftAssignmentSeriesRequestValidator()
    {
        RuleFor(request => request.ShiftSeriesId).GreaterThan(0);
        RuleFor(request => request.AssignmentSeriesId).GreaterThan(0);
        RuleFor(request => request.UserIds).NotEmpty();
        RuleFor(request => request.UserIds).Must(HaveUniqueUsers).WithMessage("Selected users must be unique.");
    }

    private static bool HaveUniqueUsers(IReadOnlyCollection<Guid> userIds) => userIds.Distinct().Count() == userIds.Count;
}
