using FluentValidation;
using Microsoft.Extensions.Options;
using Unified.UserManagement.FeatureFlags;
using Unified.UserManagement.Models;

namespace Unified.UserManagement.Validators;

public class UserRequestValidator : AbstractValidator<UserRequestDto>
{
    public UserRequestValidator(IOptionsMonitor<UserManagementFeatureFlags> featureFlagsMonitor)
    {
        RuleFor(x => x.IdirName)
            .NotEmpty()
            .WithMessage("IDIR name is required.")
            .MaximumLength(200)
            .WithMessage("IDIR name must be 200 characters or less.");

        RuleFor(x => x.FirstName)
            .NotEmpty()
            .WithMessage("First name is required.")
            .MaximumLength(150)
            .WithMessage("First name must be 150 characters or less.");

        RuleFor(x => x.LastName)
            .NotEmpty()
            .WithMessage("Last name is required.")
            .MaximumLength(150)
            .WithMessage("Last name must be 150 characters or less.");

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required.")
            .MaximumLength(320)
            .WithMessage("Email must be 320 characters or less.")
            .EmailAddress()
            .WithMessage("Email must be a valid email address.")
            .When(x => x.Email is not null);

        RuleFor(x => x.Gender).IsInEnum().WithMessage("Gender must be a valid value.");

        RuleFor(x => x.HomeLocationId).GreaterThan(0).WithMessage("Home location is required.");

        RuleFor(x => x.Rank)
            .NotEmpty()
            .WithMessage("Rank is required.")
            .MaximumLength(150)
            .WithMessage("Rank must be 150 characters or less.");

        RuleFor(x => x.BadgeNumber)
            .NotEmpty()
            .WithMessage("Badge number is required.")
            .When(_ => featureFlagsMonitor.CurrentValue.UserBadgeNumber.Required)
            .MaximumLength(100)
            .WithMessage("Badge number must be 100 characters or less.");

        RuleFor(x => x.EmployeeNumber)
            .NotEmpty()
            .WithMessage("Employee number is required.")
            .MaximumLength(100)
            .WithMessage("Employee number must be 100 characters or less.");
    }
}
