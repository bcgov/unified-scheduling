using FluentValidation;
using Unified.Api.Models;

namespace Unified.Api.Validators;

public sealed class TestEmailRequestValidator : AbstractValidator<TestEmailRequest>
{
    public TestEmailRequestValidator()
    {
        RuleFor(request => request.Recipient)
            .NotEmpty()
            .WithMessage("Recipient is required.")
            .MaximumLength(320)
            .WithMessage("Recipient must not exceed 320 characters.")
            .EmailAddress()
            .WithMessage("Recipient must be a valid email address.");
    }
}
