using FluentValidation;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Validators;

public sealed class ShiftEntryLinkRequestValidator : AbstractValidator<ShiftEntryLinkRequest>
{
    public ShiftEntryLinkRequestValidator()
    {
        RuleFor(request => request.ShiftEntryId).GreaterThan(0);
        RuleFor(request => request.AssignedUserIds).NotEmpty().Must(RelationshipLinkValidation.HaveDistinctValues);
        RuleForEach(request => request.AssignedUserIds).NotEmpty();
    }
}

public sealed class ShiftSeriesLinkRequestValidator : AbstractValidator<ShiftSeriesLinkRequest>
{
    public ShiftSeriesLinkRequestValidator()
    {
        RuleFor(request => request.ShiftSeriesId).GreaterThan(0);
        RuleFor(request => request.AssignedUserIds).NotEmpty().Must(RelationshipLinkValidation.HaveDistinctValues);
        RuleForEach(request => request.AssignedUserIds).NotEmpty();
    }
}

public sealed class AssignmentEntryLinkRequestValidator : AbstractValidator<AssignmentEntryLinkRequest>
{
    public AssignmentEntryLinkRequestValidator()
    {
        RuleFor(request => request.AssignmentEntryId).GreaterThan(0);
        RuleFor(request => request.AssignedUserIds).NotEmpty().Must(RelationshipLinkValidation.HaveDistinctValues);
        RuleForEach(request => request.AssignedUserIds).NotEmpty();
    }
}

public sealed class AssignmentSeriesLinkRequestValidator : AbstractValidator<AssignmentSeriesLinkRequest>
{
    public AssignmentSeriesLinkRequestValidator()
    {
        RuleFor(request => request.AssignmentSeriesId).GreaterThan(0);
        RuleFor(request => request.AssignedUserIds).NotEmpty().Must(RelationshipLinkValidation.HaveDistinctValues);
        RuleForEach(request => request.AssignedUserIds).NotEmpty();
    }
}

internal static class RelationshipLinkValidation
{
    internal static bool HaveDistinctValues(IReadOnlyCollection<Guid> values) =>
        values.Distinct().Count() == values.Count;
}
