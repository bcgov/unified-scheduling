using FluentValidation;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Validators;

public sealed class AssignmentSeriesRequestValidator : AbstractValidator<AssignmentSeriesRequest>
{
    public AssignmentSeriesRequestValidator()
    {
        RuleFor(request => request.Title).NotEmpty().MaximumLength(200);
        RuleFor(request => request.AssignmentDefinitionId).GreaterThan(0);
        RuleFor(request => request.EndAtUtc)
            .GreaterThan(request => request.StartAtUtc)
            .When(request => request.EndAtUtc.HasValue);
        RuleFor(request => request.LocationId).NotNull().GreaterThan(0);
        RuleFor(request => request.Capacity)
            .GreaterThanOrEqualTo(1)
            .When(request => request.Capacity.HasValue);
        RuleFor(request => request.RecurrenceRule).NotEmpty();
        RuleFor(request => request.ShiftSeriesIds)
            .Must(AssignmentRequestValidatorHelpers.HaveDistinctIds)
            .When(request => request.ShiftSeriesIds is not null);
        RuleFor(request => request.ShiftSeriesIds)
            .Null()
            .WithMessage("Use either ShiftSeriesIds or ShiftSeriesLinks, not both.")
            .When(request => request.ShiftSeriesLinks is not null);
        RuleForEach(request => request.ShiftSeriesIds)
            .GreaterThan(0)
            .When(request => request.ShiftSeriesIds is not null);
        RuleFor(request => request.AssignedUserIds)
            .NotNull()
            .WithMessage("AssignedUserIds must be provided when ShiftSeriesIds are provided.")
            .When(request => request.ShiftSeriesIds is { Count: > 0 });
        RuleFor(request => request.AssignedUserIds)
            .Null()
            .WithMessage("ShiftSeriesIds must be provided when AssignedUserIds is provided.")
            .When(request => request.ShiftSeriesIds is null);
        RuleFor(request => request.AssignedUserIds!)
            .NotEmpty()
            .Must(AssignmentRequestValidatorHelpers.HaveDistinctGuids)
            .When(request => request.ShiftSeriesIds is { Count: > 0 });
        RuleForEach(request => request.AssignedUserIds).NotEmpty();
        RuleFor(request => request.ShiftSeriesLinks)
            .Must(AssignmentRequestValidatorHelpers.HaveDistinctShiftSeriesLinks)
            .WithMessage("ShiftSeriesLinks must contain unique shift series ids.")
            .When(request => request.ShiftSeriesLinks is not null);
        RuleForEach(request => request.ShiftSeriesLinks).SetValidator(new ShiftSeriesLinkRequestValidator());
    }
}

public sealed class AssignmentEntryRequestValidator : AbstractValidator<AssignmentEntryRequest>
{
    public AssignmentEntryRequestValidator()
    {
        RuleFor(request => request.AssignmentSeriesId)
            .GreaterThan(0)
            .When(request => request.AssignmentSeriesId.HasValue);
        RuleFor(request => request.Title).NotEmpty().MaximumLength(200);
        RuleFor(request => request.AssignmentDefinitionId).GreaterThan(0);
        RuleFor(request => request.EndAtUtc)
            .GreaterThan(request => request.StartAtUtc)
            .When(request => request.EndAtUtc.HasValue);
        RuleFor(request => request.LocationId).NotNull().GreaterThan(0);
        RuleFor(request => request.Capacity)
            .GreaterThanOrEqualTo(1)
            .When(request => request.Capacity.HasValue);
        RuleFor(request => request.ShiftEntryIds)
            .Must(AssignmentRequestValidatorHelpers.HaveDistinctIds)
            .When(request => request.ShiftEntryIds is not null);
        RuleFor(request => request.ShiftEntryIds)
            .Null()
            .WithMessage("Use either ShiftEntryIds or ShiftEntryLinks, not both.")
            .When(request => request.ShiftEntryLinks is not null);
        RuleForEach(request => request.ShiftEntryIds)
            .GreaterThan(0)
            .When(request => request.ShiftEntryIds is not null);
        RuleFor(request => request.AssignedUserIds)
            .NotNull()
            .WithMessage("AssignedUserIds must be provided when ShiftEntryIds are provided.")
            .When(request => request.ShiftEntryIds is { Count: > 0 });
        RuleFor(request => request.AssignedUserIds)
            .Null()
            .WithMessage("ShiftEntryIds must be provided when AssignedUserIds is provided.")
            .When(request => request.ShiftEntryIds is null);
        RuleFor(request => request.AssignedUserIds!)
            .NotEmpty()
            .Must(AssignmentRequestValidatorHelpers.HaveDistinctGuids)
            .When(request => request.ShiftEntryIds is { Count: > 0 });
        RuleForEach(request => request.AssignedUserIds).NotEmpty();
        RuleFor(request => request.ShiftEntryLinks)
            .Must(AssignmentRequestValidatorHelpers.HaveDistinctShiftEntryLinks)
            .WithMessage("ShiftEntryLinks must contain unique shift entry ids.")
            .When(request => request.ShiftEntryLinks is not null);
        RuleForEach(request => request.ShiftEntryLinks).SetValidator(new ShiftEntryLinkRequestValidator());
    }

}

public sealed class ShiftEntryLinkRequestValidator : AbstractValidator<ShiftEntryLinkRequest>
{
    public ShiftEntryLinkRequestValidator()
    {
        RuleFor(request => request.ShiftEntryId).GreaterThan(0);
        RuleFor(request => request.AssignedUserIds)
            .NotEmpty()
            .Must(AssignmentRequestValidatorHelpers.HaveDistinctGuids)
            .WithMessage("Selected users must be unique.");
        RuleForEach(request => request.AssignedUserIds).NotEmpty();
    }
}

public sealed class ShiftSeriesLinkRequestValidator : AbstractValidator<ShiftSeriesLinkRequest>
{
    public ShiftSeriesLinkRequestValidator()
    {
        RuleFor(request => request.ShiftSeriesId).GreaterThan(0);
        RuleFor(request => request.AssignedUserIds)
            .NotEmpty()
            .Must(AssignmentRequestValidatorHelpers.HaveDistinctGuids)
            .WithMessage("Selected users must be unique.");
        RuleForEach(request => request.AssignedUserIds).NotEmpty();
    }
}

file static class AssignmentRequestValidatorHelpers
{
    public static bool HaveDistinctIds(IReadOnlyCollection<int>? ids)
    {
        if (ids is null)
            return true;

        return ids.Distinct().Count() == ids.Count;
    }

    public static bool HaveDistinctGuids(IReadOnlyCollection<Guid>? ids)
    {
        if (ids is null)
            return false;

        return ids.Distinct().Count() == ids.Count;
    }

    public static bool HaveDistinctShiftEntryLinks(IReadOnlyCollection<ShiftEntryLinkRequest>? links)
    {
        if (links is null)
            return true;

        return links.Select(link => link.ShiftEntryId).Distinct().Count() == links.Count;
    }

    public static bool HaveDistinctShiftSeriesLinks(IReadOnlyCollection<ShiftSeriesLinkRequest>? links)
    {
        if (links is null)
            return true;

        return links.Select(link => link.ShiftSeriesId).Distinct().Count() == links.Count;
    }
}
