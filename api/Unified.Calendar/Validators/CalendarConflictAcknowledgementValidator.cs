using FluentValidation;
using Unified.Calendar.Models;

namespace Unified.Calendar.Validators;

public sealed class CalendarConflictAcknowledgementValidator : AbstractValidator<CalendarConflictAcknowledgement>
{
    public CalendarConflictAcknowledgementValidator()
    {
        RuleFor(acknowledgement => acknowledgement.FirstEventId).GreaterThan(0);
        RuleFor(acknowledgement => acknowledgement.SecondEventId)
            .GreaterThan(0)
            .NotEqual(acknowledgement => acknowledgement.FirstEventId);
        RuleFor(acknowledgement => acknowledgement.ResourceId).NotEmpty();
        RuleFor(acknowledgement => acknowledgement.Note).NotEmpty().MaximumLength(2000);
    }
}
