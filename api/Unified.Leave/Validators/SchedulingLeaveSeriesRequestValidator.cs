using FluentValidation;
using FluentValidation.Results;
using Unified.Calendar.Services;
using Unified.Common.Time;
using Unified.Leave.Models.Scheduling;

namespace Unified.Leave.Validators;

public sealed class SchedulingLeaveSeriesRequestValidator : AbstractValidator<SchedulingLeaveSeriesRequest>
{
    public SchedulingLeaveSeriesRequestValidator(IRecurrenceRuleValidator recurrenceRuleValidator)
    {
        Include(new SchedulingLeaveDateValidator<SchedulingLeaveSeriesRequest>());
        RuleFor(request => request.LeaveTypeId).NotEmpty().GreaterThan(0);
        RuleFor(request => request.Title).NotEmpty().MaximumLength(200);
        RuleFor(request => request.RecurrenceRule).NotEmpty();
        RuleFor(request => request.Description).MaximumLength(2000);
        RuleFor(request => request.Notes).MaximumLength(4000);
        RuleFor(request => request.Color).MaximumLength(100);
        RuleFor(request => request.TimeZoneId).MaximumLength(100).Must(TimeZoneService.IsValidTimeZoneId);
        RuleFor(request => request.LocationId).GreaterThan(0);
        RuleFor(request => request.UserIds)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(userIds => userIds.Distinct().Count() == userIds.Count)
            .WithMessage("Users must be unique.");
        RuleForEach(request => request.UserIds).NotEmpty();
        RuleFor(request => request.RecurrenceRule)
            .Must(rule =>
                string.IsNullOrWhiteSpace(rule)
                || !rule.Replace("RRULE:", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Split(';')
                    .Select(part => part.Trim().ToUpperInvariant())
                    .Any(part =>
                        part.StartsWith("BYHOUR=")
                        || part.StartsWith("BYMINUTE=")
                        || part.StartsWith("BYSECOND=")
                        || part is "FREQ=HOURLY" or "FREQ=MINUTELY" or "FREQ=SECONDLY"
                    )
            )
            .When(request => request.AllDay)
            .WithMessage("All-day leave recurrence must use calendar dates without time-of-day rules.");
        RuleFor(request => request.RecurrenceRule)
            .NotEmpty()
            .Custom(
                (rule, context) =>
                {
                    if (
                        string.IsNullOrWhiteSpace(rule)
                        || context.InstanceToValidate.AllDay
                        || !context.InstanceToValidate.StartAtUtc.HasValue
                    )
                        return;

                    var request = (SchedulingLeaveSeriesRequest)context.InstanceToValidate;
                    RecurrenceValidationResult result;
                    try
                    {
                        result = recurrenceRuleValidator.Validate(
                            rule,
                            request.StartAtUtc.Value,
                            request.EndAtUtc,
                            request.TimeZoneId,
                            new RecurrenceValidationOptions
                            {
                                MaximumDuration = TimeSpan.FromDays(365),
                                MaximumOccurrences = 400,
                                RequireBoundedRule = true,
                            }
                        );
                    }
                    catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
                    {
                        context.AddFailure(
                            nameof(request.RecurrenceRule),
                            $"The recurrence rule is invalid: {exception.Message}"
                        );
                        return;
                    }

                    foreach (var error in result.Errors)
                        context.AddFailure(new ValidationFailure(nameof(request.RecurrenceRule), error));
                }
            );
    }
}
