using FluentValidation;
using FluentValidation.Results;
using Unified.Calendar.Services;
using Unified.Common.Time;
using Unified.TimeOff.Models.Scheduling;

namespace Unified.TimeOff.Validators;

public sealed class SchedulingTimeOffSeriesRequestValidator : AbstractValidator<SchedulingTimeOffSeriesRequest>
{
    public SchedulingTimeOffSeriesRequestValidator(IRecurrenceRuleValidator recurrenceRuleValidator)
    {
        RuleFor(request => request.LeaveTypeId).NotEmpty().GreaterThan(0);
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
        RuleFor(request => request.LocationId).GreaterThan(0);
        RuleFor(request => request.UserIds)
            .NotEmpty()
            .Must(userIds => userIds.Distinct().Count() == userIds.Count)
            .WithMessage("Users must be unique.");
        RuleForEach(request => request.UserIds).NotEmpty();
        RuleFor(request => request.RecurrenceRule)
            .NotEmpty()
            .Custom(
                (rule, context) =>
                {
                    if (string.IsNullOrWhiteSpace(rule))
                        return;

                    var request = (SchedulingTimeOffSeriesRequest)context.InstanceToValidate;
                    RecurrenceValidationResult result;
                    try
                    {
                        result = recurrenceRuleValidator.Validate(
                            rule,
                            request.StartAtUtc,
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
