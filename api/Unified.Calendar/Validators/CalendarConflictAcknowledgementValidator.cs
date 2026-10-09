using FluentValidation;
using Unified.Calendar.Models;

namespace Unified.Calendar.Validators;

public sealed class CalendarConflictAcknowledgementValidator : AbstractValidator<CalendarConflictAcknowledgement>
{
    public CalendarConflictAcknowledgementValidator()
    {
        RuleFor(acknowledgement => acknowledgement.FirstSourceModule).NotEmpty();
        RuleFor(acknowledgement => acknowledgement.FirstEventId).NotEmpty();
        RuleFor(acknowledgement => acknowledgement.SecondSourceModule).NotEmpty();
        RuleFor(acknowledgement => acknowledgement.SecondEventId).NotEmpty();
        RuleFor(acknowledgement => acknowledgement)
            .Must(acknowledgement =>
                acknowledgement.FirstSourceModule != acknowledgement.SecondSourceModule
                || acknowledgement.FirstEventId != acknowledgement.SecondEventId
            )
            .WithMessage("A conflict acknowledgement requires two different calendar events.");
        RuleFor(acknowledgement => acknowledgement.ResourceId).NotEmpty();
        RuleFor(acknowledgement => acknowledgement.Note).NotEmpty().MaximumLength(2000);
    }
}
