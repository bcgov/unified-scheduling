using FluentValidation;
using Unified.UserManagement.Models;

namespace Unified.UserManagement.Validators;

public class ExpireActingPositionRequestValidator : AbstractValidator<ExpireActingPositionRequestDto>
{
    public ExpireActingPositionRequestValidator()
    {
        RuleFor(x => x.ActingPositionId).GreaterThan(0).WithMessage("ActingPositionId is required.");

        RuleFor(x => x.ExpiryReason)
            .NotEmpty()
            .WithMessage("ExpiryReason is required.")
            .MaximumLength(200)
            .WithMessage("ExpiryReason must not exceed 200 characters.");
    }
}
