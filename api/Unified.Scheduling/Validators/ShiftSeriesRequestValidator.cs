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
        RuleFor(request => request.LocationId).GreaterThan(0).When(request => request.LocationId.HasValue);
        RuleFor(request => request.UserIds).NotEmpty().Must(HaveDistinctValues);
        RuleForEach(request => request.UserIds).NotEmpty();
        RuleFor(request => request.AssignmentSeriesId)
            .GreaterThan(0)
            .When(request => request.AssignmentSeriesId.HasValue);
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
