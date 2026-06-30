using FluentValidation;
using Unified.UserManagement.Models;

namespace Unified.UserManagement.Validators;

public class ExpireAwayLocationRequestValidator : AbstractValidator<ExpireAwayLocationRequestDto>
{
    public ExpireAwayLocationRequestValidator()
    {
        RuleFor(x => x.AwayLocationId).GreaterThan(0).WithMessage("AwayLocationId is required.");

        RuleFor(x => x.ExpiryReason)
            .NotEmpty()
            .WithMessage("ExpiryReason is required.")
            .MaximumLength(200)
            .WithMessage("ExpiryReason must not exceed 200 characters.");
    }
}
