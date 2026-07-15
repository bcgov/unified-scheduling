using FluentValidation;
using Unified.Db.Models.Calendar;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Validators;

public sealed class ShiftEntryRequestValidator : AbstractValidator<ShiftEntryRequest>
{
    public ShiftEntryRequestValidator()
    {
        RuleFor(request => request.ShiftSeriesId).GreaterThan(0).When(request => request.ShiftSeriesId.HasValue);
        RuleFor(request => request.Title).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Description).MaximumLength(2000);
        RuleFor(request => request.Notes).MaximumLength(4000);
        RuleFor(request => request.Color).MaximumLength(100);
        RuleFor(request => request.TimeZoneId).MaximumLength(100);
        RuleFor(request => request.StartAtUtc)
            .LessThan(request => request.EndAtUtc!.Value)
            .When(request => request.EndAtUtc.HasValue);
        RuleFor(request => request.SeriesStartAtUtc)
            .LessThan(request => request.SeriesEndAtUtc!.Value)
            .When(request => request.SeriesStartAtUtc.HasValue && request.SeriesEndAtUtc.HasValue);
        RuleFor(request => request.StatusTypeCode)
            .Must(BeValidStatusTypeCode)
            .When(request => !string.IsNullOrWhiteSpace(request.StatusTypeCode));
        RuleFor(request => request.LocationId).NotNull().GreaterThan(0);
        RuleFor(request => request.UserIds).NotEmpty().Must(HaveDistinctValues);
        RuleForEach(request => request.UserIds).NotEmpty();
        RuleFor(request => request.AssignmentEntryIds).Must(HaveDistinctIds).When(request => request.AssignmentEntryIds is not null);
        RuleFor(request => request.AssignmentEntryIds)
            .Null()
            .WithMessage("Use either AssignmentEntryIds or AssignmentEntryLinks, not both.")
            .When(request => request.AssignmentEntryLinks is not null);
        RuleForEach(request => request.AssignmentEntryIds).GreaterThan(0).When(request => request.AssignmentEntryIds is not null);
        RuleFor(request => request.AssignedUserIds)
            .NotNull()
            .WithMessage("AssignedUserIds must be provided when AssignmentEntryIds are provided.")
            .When(request => request.AssignmentEntryIds is { Count: > 0 });
        RuleFor(request => request.AssignedUserIds)
            .Null()
            .WithMessage("AssignmentEntryIds must be provided when AssignedUserIds is provided.")
            .When(request => request.AssignmentEntryIds is null);
        RuleFor(request => request.AssignedUserIds!)
            .NotEmpty()
            .Must(HaveDistinctValues)
            .When(request => request.AssignmentEntryIds is { Count: > 0 });
        RuleForEach(request => request.AssignedUserIds).NotEmpty();
        RuleFor(request => request.AssignmentEntryLinks)
            .Must(HaveDistinctAssignmentEntryLinks)
            .WithMessage("AssignmentEntryLinks must contain unique assignment entry ids.")
            .When(request => request.AssignmentEntryLinks is not null);
        RuleForEach(request => request.AssignmentEntryLinks).SetValidator(new AssignmentEntryLinkRequestValidator());
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

    private static bool HaveDistinctAssignmentEntryLinks(IReadOnlyCollection<AssignmentEntryLinkRequest>? links)
    {
        if (links is null)
            return true;

        return links.Select(link => link.AssignmentEntryId).Distinct().Count() == links.Count;
    }

    private static bool BeValidStatusTypeCode(string? statusTypeCode)
    {
        return statusTypeCode?.Trim() == CalendarEventStatusTypeCodes.Draft;
    }
}

public sealed class AssignmentEntryLinkRequestValidator : AbstractValidator<AssignmentEntryLinkRequest>
{
    public AssignmentEntryLinkRequestValidator()
    {
        RuleFor(request => request.AssignmentEntryId).GreaterThan(0);
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
