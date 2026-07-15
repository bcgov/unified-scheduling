using FluentValidation;
using Unified.Db.Models.Calendar;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Validators;

public sealed class ShiftSeriesRequestValidator : AbstractValidator<ShiftSeriesRequest>
{
    public ShiftSeriesRequestValidator()
    {
        RuleFor(request => request.Title).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Description).MaximumLength(2000);
        RuleFor(request => request.Notes).MaximumLength(4000);
        RuleFor(request => request.Color).MaximumLength(100);
        RuleFor(request => request.TimeZoneId).MaximumLength(100);
        RuleFor(request => request.StartAtUtc)
            .LessThanOrEqualTo(request => request.EndAtUtc!.Value)
            .When(request => request.EndAtUtc.HasValue);
        RuleFor(request => request.StatusTypeCode)
            .Must(BeValidStatusTypeCode)
            .When(request => !string.IsNullOrWhiteSpace(request.StatusTypeCode));
        RuleFor(request => request.LocationId).NotNull().GreaterThan(0);
        RuleFor(request => request.UserIds).NotEmpty().Must(HaveDistinctValues);
        RuleForEach(request => request.UserIds).NotEmpty();
        RuleFor(request => request.AssignmentSeriesIds).Must(HaveDistinctIds).When(request => request.AssignmentSeriesIds is not null);
        RuleFor(request => request.AssignmentSeriesIds)
            .Null()
            .WithMessage("Use either AssignmentSeriesIds or AssignmentSeriesLinks, not both.")
            .When(request => request.AssignmentSeriesLinks is not null);
        RuleForEach(request => request.AssignmentSeriesIds).GreaterThan(0).When(request => request.AssignmentSeriesIds is not null);
        RuleFor(request => request.AssignedUserIds)
            .NotNull()
            .NotEmpty()
            .When(request => request.AssignmentSeriesIds?.Count > 0)
            .WithMessage("AssignedUserIds must be provided when AssignmentSeriesIds are provided.");
        RuleFor(request => request.AssignedUserIds)
            .Null()
            .When(request => request.AssignmentSeriesIds is null)
            .WithMessage("AssignmentSeriesIds must be provided when AssignedUserIds is provided.");
        RuleFor(request => request.AssignedUserIds!)
            .Must(HaveDistinctValues)
            .When(request => request.AssignedUserIds is not null);
        RuleFor(request => request.AssignedUserIds!)
            .Must((request, assignedUserIds) => assignedUserIds.All(request.UserIds.Contains))
            .When(request => request.AssignedUserIds is not null)
            .WithMessage("Assigned users must belong to the shift users.");
        RuleForEach(request => request.AssignedUserIds).NotEmpty();
        RuleFor(request => request.AssignmentSeriesLinks)
            .Must(HaveDistinctAssignmentSeriesLinks)
            .WithMessage("AssignmentSeriesLinks must contain unique assignment series ids.")
            .When(request => request.AssignmentSeriesLinks is not null);
        RuleForEach(request => request.AssignmentSeriesLinks).SetValidator(new AssignmentSeriesLinkRequestValidator());
    }

    private static bool HaveDistinctValues(IReadOnlyCollection<Guid>? userIds)
    {
        if (userIds is null)
            return false;

        return userIds.Distinct().Count() == userIds.Count;
    }

    private static bool HaveDistinctIds(IReadOnlyCollection<int>? ids)
    {
        if (ids is null)
            return true;

        return ids.Distinct().Count() == ids.Count;
    }

    private static bool HaveDistinctAssignmentSeriesLinks(IReadOnlyCollection<AssignmentSeriesLinkRequest>? links)
    {
        if (links is null)
            return true;

        return links.Select(link => link.AssignmentSeriesId).Distinct().Count() == links.Count;
    }

    private static bool BeValidStatusTypeCode(string? statusTypeCode)
    {
        return statusTypeCode?.Trim() == CalendarEventStatusTypeCodes.Draft;
    }
}

public sealed class AssignmentSeriesLinkRequestValidator : AbstractValidator<AssignmentSeriesLinkRequest>
{
    public AssignmentSeriesLinkRequestValidator()
    {
        RuleFor(request => request.AssignmentSeriesId).GreaterThan(0);
        RuleFor(request => request.AssignedUserIds)
            .NotEmpty()
            .Must(HaveDistinctValues)
            .WithMessage("Selected users must be unique.");
        RuleForEach(request => request.AssignedUserIds).NotEmpty();
    }

    private static bool HaveDistinctValues(IReadOnlyCollection<Guid>? userIds)
    {
        if (userIds is null)
            return false;

        return userIds.Distinct().Count() == userIds.Count;
    }
}
