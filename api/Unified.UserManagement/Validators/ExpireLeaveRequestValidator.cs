using FluentValidation;
using Unified.UserManagement.Models;

namespace Unified.UserManagement.Validators;

public class ExpireLeaveRequestValidator : AbstractValidator<ExpireLeaveRequestDto>
{
    public ExpireLeaveRequestValidator()
    {
        RuleFor(x => x.LeaveId).GreaterThan(0).WithMessage("LeaveId is required.");

        RuleFor(x => x.ExpiryReason)
            .NotEmpty()
            .WithMessage("ExpiryReason is required.")
            .MaximumLength(200)
            .WithMessage("ExpiryReason must not exceed 200 characters.");
    }
}
