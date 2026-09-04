using FluentValidation;
using Microsoft.Extensions.Options;
using Unified.Common.Time;
using Unified.Scheduling.Models;
using Unified.Scheduling.Options;

namespace Unified.Scheduling.Validators;

public sealed class WorkingHoursQueryValidator
    : AbstractValidator<WorkingHoursQuery>
{
    public WorkingHoursQueryValidator(
        IOptions<WorkingHoursOptions> options)
    {
        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate);

        RuleFor(x => x)
            .Must(x =>
                x.EndDate.DayNumber - x.StartDate.DayNumber + 1
                <= options.Value.MaxQueryRangeDays)
            .WithMessage(
                $"Date range cannot exceed " +
                $"{options.Value.MaxQueryRangeDays} days.");
    }
}