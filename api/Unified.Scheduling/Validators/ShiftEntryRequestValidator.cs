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
        RuleFor(request => request.LocationId).GreaterThan(0).When(request => request.LocationId.HasValue);
        RuleFor(request => request.UserIds).NotEmpty().Must(HaveDistinctValues);
        RuleForEach(request => request.UserIds).NotEmpty();
        RuleFor(request => request.AssignmentEntryId).GreaterThan(0).When(request => request.AssignmentEntryId.HasValue);
        RuleFor(request => request.AssignedUserIds)
            .NotNull()
            .WithMessage("AssignedUserIds must be provided when AssignmentEntryId is provided.")
            .When(request => request.AssignmentEntryId.HasValue);
        RuleFor(request => request.AssignedUserIds)
            .Null()
            .WithMessage("AssignmentEntryId must be provided when AssignedUserIds is provided.")
            .When(request => !request.AssignmentEntryId.HasValue);
        RuleFor(request => request.AssignedUserIds!)
            .NotEmpty()
            .Must(HaveDistinctValues)
            .When(request => request.AssignmentEntryId.HasValue);
        RuleForEach(request => request.AssignedUserIds).NotEmpty();
    }

    private static bool HaveDistinctValues(IReadOnlyCollection<Guid>? userIds)
    {
        if (userIds is null)
            return false;

        return userIds.Distinct().Count() == userIds.Count;
    }

    private static bool BeValidStatusTypeCode(string? statusTypeCode)
    {
        return statusTypeCode?.Trim() == CalendarEventStatusTypeCodes.Draft;
    }
}
