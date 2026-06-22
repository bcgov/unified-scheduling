using FluentValidation;
using Unified.Common.Helpers.Extensions;
using Unified.UserManagement.Models;

namespace Unified.UserManagement.Validators;

public sealed class DeleteRoleWithReassignmentRequestDtoValidator
    : AbstractValidator<DeleteRoleWithReassignmentRequestDto>
{
    public DeleteRoleWithReassignmentRequestDtoValidator()
    {
        // NewRoleId is required and must be a positive integer
        RuleFor(x => x.NewRoleId).NotNull().GreaterThan(0).WithMessage("New role ID must be valid");

        // NewRoleEffectiveDate is required and must be yyyy-MM-dd
        RuleFor(x => x.NewRoleEffectiveDate)
            .NotEmpty()
            .WithMessage("Effective date is required")
            .Must(x => DateTimeOffsetExtensions.IsValidDateFormat(x!, DateTimeOffsetExtensions.DateFormat))
            .When(x => !string.IsNullOrEmpty(x.NewRoleEffectiveDate))
            .WithMessage($"NewRoleEffectiveDate must be in {DateTimeOffsetExtensions.DateFormat} format");

        // NewRoleExpiryDate format: must be yyyy-MM-dd when provided
        RuleFor(x => x.NewRoleExpiryDate)
            .Must(x => DateTimeOffsetExtensions.IsValidDateFormat(x!, DateTimeOffsetExtensions.DateFormat))
            .When(x => !string.IsNullOrEmpty(x.NewRoleExpiryDate))
            .WithMessage($"NewRoleExpiryDate must be in {DateTimeOffsetExtensions.DateFormat} format");

        // Cross-field: expiry must be after effective date when both are provided and valid
        RuleFor(x => x)
            .Must(x =>
                string.IsNullOrEmpty(x.NewRoleExpiryDate)
                || DateOnly.ParseExact(x.NewRoleExpiryDate, DateTimeOffsetExtensions.DateFormat)
                    > DateOnly.ParseExact(x.NewRoleEffectiveDate!, DateTimeOffsetExtensions.DateFormat)
            )
            .When(x =>
                DateTimeOffsetExtensions.IsValidDateFormat(x.NewRoleEffectiveDate, DateTimeOffsetExtensions.DateFormat)
                && DateTimeOffsetExtensions.IsValidDateFormat(x.NewRoleExpiryDate, DateTimeOffsetExtensions.DateFormat)
            )
            .WithName(nameof(DeleteRoleWithReassignmentRequestDto.NewRoleExpiryDate))
            .WithMessage("Expiry date must be after the effective date");
    }
}
