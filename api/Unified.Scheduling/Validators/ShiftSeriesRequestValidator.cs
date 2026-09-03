using FluentValidation;
using Unified.Common.Time;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Validators;

public sealed class ShiftSeriesRequestValidator : AbstractValidator<ShiftSeriesRequest>
{
    public ShiftSeriesRequestValidator()
    {
        RuleFor(request => request.Title).NotEmpty().MaximumLength(200);
        RuleFor(request => request.RecurrenceRule).NotEmpty();
        RuleFor(request => request.Description).MaximumLength(2000);
        RuleFor(request => request.Notes).MaximumLength(4000);
        RuleFor(request => request.Color).MaximumLength(100);
        RuleFor(request => request.TimeZoneId).MaximumLength(100).Must(TimeZoneService.IsValidTimeZoneId);
        RuleFor(request => request.StartAtUtc).NotEqual(default(DateTimeOffset));
        RuleFor(request => request.EndAtUtc).NotNull();
        RuleFor(request => request.StartAtUtc)
            .LessThan(request => request.EndAtUtc!.Value)
            .When(request => request.EndAtUtc.HasValue);
        RuleFor(request => request.LocationId).GreaterThan(0).When(request => request.LocationId.HasValue);
        RuleFor(request => request.UserIds).NotEmpty().Must(HaveDistinctValues);
        RuleForEach(request => request.UserIds).NotEmpty();
        RuleFor(request => request.AssignmentSeriesLinks)
            .Must(links => links.Select(link => link.AssignmentSeriesId).Distinct().Count() == links.Count)
            .WithMessage("Assignment series links must be unique.");
        RuleForEach(request => request.AssignmentSeriesLinks).SetValidator(new AssignmentSeriesLinkRequestValidator());
    }

    private static bool HaveDistinctValues(IReadOnlyCollection<Guid>? userIds)
    {
        if (userIds is null)
            return false;

        return userIds.Distinct().Count() == userIds.Count;
    }
}
