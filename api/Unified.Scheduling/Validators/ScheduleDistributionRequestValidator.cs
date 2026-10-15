using FluentValidation;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Validators;

public sealed class ScheduleDistributionRequestValidator : AbstractValidator<ScheduleDistributionRequest>
{
    private static readonly DateOnly MinimumDate = new(1900, 1, 1);
    private const int MaximumRangeLengthDays = 366;
    private const int MaximumRecipientCount = 500;

    public ScheduleDistributionRequestValidator()
    {
        RuleFor(request => request.Context).NotNull();
        RuleFor(request => request.RecipientUserIds).NotNull().NotEmpty().Must(HaveUniqueRecipients);
        RuleForEach(request => request.RecipientUserIds!).NotEmpty().When(request => request.RecipientUserIds is not null);
        RuleFor(request => request.RecipientUserIds)
            .Must(recipients => recipients is null || recipients.Count <= MaximumRecipientCount)
            .When(request => request.RecipientUserIds is not null);

        When(
            request => request.Context is not null,
            () =>
            {
                RuleFor(request => request.Context!.View).Must(BeSupportedView);
                RuleFor(request => request.Context!.LocationId).GreaterThan(0);
                RuleFor(request => request.Context!.StartDate).GreaterThan(MinimumDate);
                RuleFor(request => request.Context!.EndDate).GreaterThan(request => request.Context!.StartDate);
                RuleFor(request => request.Context!.EndDate)
                    .Must(
                        (request, endDate) =>
                            endDate.DayNumber - request.Context!.StartDate.DayNumber <= MaximumRangeLengthDays
                    );
                RuleFor(request => request.Context!.Period).Must(BeSupportedPeriod);
                RuleFor(request => request.Context!).Must(HaveExpectedPeriodRange);
            }
        );
    }

    private static bool HaveUniqueRecipients(IReadOnlyCollection<Guid>? recipientUserIds) =>
        recipientUserIds is not null && recipientUserIds.Count == recipientUserIds.Distinct().Count();

    private static bool BeSupportedView(string? view) =>
        string.Equals(view, "schedule", StringComparison.OrdinalIgnoreCase)
        || string.Equals(view, "assignments", StringComparison.OrdinalIgnoreCase);

    private static bool BeSupportedPeriod(string? period) =>
        string.Equals(period, "day", StringComparison.OrdinalIgnoreCase)
        || string.Equals(period, "week", StringComparison.OrdinalIgnoreCase)
        || string.Equals(period, "work-week", StringComparison.OrdinalIgnoreCase);

    private static bool HaveExpectedPeriodRange(ScheduleDistributionContext context)
    {
        var expectedRangeLength = context.Period?.ToLowerInvariant() switch
        {
            "day" => 1,
            "week" => 7,
            "work-week" => 5,
            _ => 0,
        };

        return expectedRangeLength == 0
            || (
                context.EndDate.DayNumber - context.StartDate.DayNumber == expectedRangeLength
                && (expectedRangeLength == 1 || context.StartDate.DayOfWeek == DayOfWeek.Monday)
            );
    }
}
