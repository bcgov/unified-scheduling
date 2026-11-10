using FluentValidation;
using Unified.Common.Validation;
using Unified.Training.Helpers;
using Unified.Training.Models;

namespace Unified.Training.Validators;

public sealed class UserTrainingRequestValidator : AbstractValidator<UserTrainingRequest>
{
    public UserTrainingRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.TrainingId)
            .NotEmpty()
            .GreaterThan(0)
            .WithMessage("Training ID is required and must be greater than 0.");

        RuleFor(x => x.AwardedOn).NotEmpty().WithMessage("Awarded date is required.");

        RuleFor(x => x.EndingOn).NotEmpty().WithMessage("Ending date is required.");

        RuleFor(x => x.EndingOn)
            .Must((request, end) => request.AllDay ? end.Date >= request.AwardedOn.Date : end > request.AwardedOn)
            .WithMessage(request =>
                request.AllDay
                    ? "Ending date must be on or after the awarded date."
                    : "Ending date must be after the awarded date."
            );

        RuleFor(x => x.ExpiryDate)
            .GreaterThan(x => UserTrainingHelper.NormalizeToUtc(x).EndingOn)
            .WithMessage("Expiry date must be after the ending date.")
            .When(x => x.ExpiryDate.HasValue);

        RuleFor(x => x.Notes)
            .MaximumLength(2000)
            .WithMessage("Notes cannot exceed 2000 characters.")
            .When(x => x.Notes is not null);
    }
}
