using FluentValidation;
using Unified.Common.Time;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Validators;

public sealed class AssignmentSeriesRequestValidator : AbstractValidator<AssignmentSeriesRequest>
{
    public AssignmentSeriesRequestValidator()
    {
        RuleFor(request => request.AssignmentDefinitionId).GreaterThan(0);
        RuleFor(request => request.Title).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Description).MaximumLength(2000);
        RuleFor(request => request.Notes).MaximumLength(4000);
        RuleFor(request => request.Color).NotEmpty().MaximumLength(100);
        RuleFor(request => request.TimeZoneId).MaximumLength(100).Must(TimeZoneService.IsValidTimeZoneId);
        RuleFor(request => request.StartAtUtc).NotEmpty();
        RuleFor(request => request.EndAtUtc).NotEmpty().GreaterThan(request => request.StartAtUtc);
        RuleFor(request => request.LocationId).GreaterThan(0);
        RuleFor(request => request.CategoryId).GreaterThan(0);
        RuleFor(request => request.SubCategoryId).GreaterThan(0);
        RuleFor(request => request.Capacity).GreaterThanOrEqualTo(1);
        RuleFor(request => request.RecurrenceRule).NotEmpty();
        RuleForEach(request => request.ShiftSeriesLinks).SetValidator(new ShiftSeriesLinkRequestValidator());
        RuleFor(request => request.ShiftSeriesLinks)
            .Must(links => links.Select(link => link.ShiftSeriesId).Distinct().Count() == links.Count)
            .WithMessage("Shift series links must be unique.");
    }
}

public sealed class AssignmentEntryRequestValidator : AbstractValidator<AssignmentEntryRequest>
{
    public AssignmentEntryRequestValidator()
    {
        RuleFor(request => request.AssignmentSeriesId)
            .GreaterThan(0)
            .When(request => request.AssignmentSeriesId.HasValue);
        RuleFor(request => request.AssignmentDefinitionId).GreaterThan(0);
        RuleFor(request => request.Title).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Description).MaximumLength(2000);
        RuleFor(request => request.Notes).MaximumLength(4000);
        RuleFor(request => request.Color).NotEmpty().MaximumLength(100);
        RuleFor(request => request.TimeZoneId).MaximumLength(100).Must(TimeZoneService.IsValidTimeZoneId);
        RuleFor(request => request.StartAtUtc).NotEmpty();
        RuleFor(request => request.EndAtUtc).NotEmpty().GreaterThan(request => request.StartAtUtc);
        RuleFor(request => request.SeriesStartAtUtc)
            .LessThan(request => request.SeriesEndAtUtc!.Value)
            .When(request => request.SeriesStartAtUtc.HasValue && request.SeriesEndAtUtc.HasValue);
        RuleFor(request => request.LocationId).GreaterThan(0);
        RuleFor(request => request.CategoryId).GreaterThan(0);
        RuleFor(request => request.SubCategoryId).GreaterThan(0);
        RuleFor(request => request.Capacity).GreaterThanOrEqualTo(1);
        AddShiftEntryLinkRules();
    }

    private void AddShiftEntryLinkRules()
    {
        RuleFor(request => request.ShiftEntryLinks)
            .Must(links => links.Select(link => link.ShiftEntryId).Distinct().Count() == links.Count)
            .WithMessage("Shift entry links must be unique.");
        RuleForEach(request => request.ShiftEntryLinks).SetValidator(new ShiftEntryLinkRequestValidator());
    }
}

public sealed class AssignmentEntryUpdateRequestValidator : AbstractValidator<AssignmentEntryUpdateRequest>
{
    public AssignmentEntryUpdateRequestValidator()
    {
        RuleFor(request => request.AssignmentDefinitionId).GreaterThan(0);
        RuleFor(request => request.Title).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Description).MaximumLength(2000);
        RuleFor(request => request.Notes).MaximumLength(4000);
        RuleFor(request => request.Color).NotEmpty().MaximumLength(100);
        RuleFor(request => request.TimeZoneId).MaximumLength(100).Must(TimeZoneService.IsValidTimeZoneId);
        RuleFor(request => request.StartAtUtc).NotEmpty();
        RuleFor(request => request.EndAtUtc).NotEmpty().GreaterThan(request => request.StartAtUtc);
        RuleFor(request => request.LocationId).GreaterThan(0);
        RuleFor(request => request.CategoryId).GreaterThan(0);
        RuleFor(request => request.SubCategoryId).GreaterThan(0);
        RuleFor(request => request.Capacity).GreaterThanOrEqualTo(1);
        AddShiftEntryLinkRules();
    }

    private void AddShiftEntryLinkRules()
    {
        RuleFor(request => request.ShiftEntryLinks)
            .Must(links => links.Select(link => link.ShiftEntryId).Distinct().Count() == links.Count)
            .WithMessage("Shift entry links must be unique.");
        RuleForEach(request => request.ShiftEntryLinks).SetValidator(new ShiftEntryLinkRequestValidator());
    }
}
