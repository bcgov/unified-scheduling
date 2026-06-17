using FluentValidation;
using Unified.UserManagement.Models;

namespace Unified.UserManagement.Validators;

/// <summary>
/// Validator for DeleteRoleWithReassignmentRequestDto.
/// </summary>
public sealed class DeleteRoleWithReassignmentRequestDtoValidator
    : AbstractValidator<DeleteRoleWithReassignmentRequestDto>
{
    /// <summary>
    /// Initializes a new instance of the DeleteRoleWithReassignmentRequestDtoValidator class.
    /// </summary>
    public DeleteRoleWithReassignmentRequestDtoValidator()
    {
        // If NewRoleId is provided, EffectiveDate is required
        RuleFor(x => x.NewRoleEffectiveDate)
            .NotEmpty()
            .WithMessage("Effective date is required when reassigning to a new role");

        // NewRoleId must be greater than 0 if provided
        RuleFor(x => x.NewRoleId).GreaterThan(0).WithMessage("New role ID must be valid");

        // ExpiryDate must be after EffectiveDate if both provided
        RuleFor(x => x.NewRoleExpiryDate)
            .Must(
                (req, expiryDate) =>
                {
                    if (string.IsNullOrEmpty(expiryDate))
                        return true;

                    if (!DateTimeOffset.TryParse(req.NewRoleEffectiveDate, out var effectiveDate))
                        return false;

                    if (!DateTimeOffset.TryParse(expiryDate, out var expiry))
                        return false;

                    return expiry > effectiveDate;
                }
            )
            .WithMessage("Expiry date must be after the effective date");
    }
}
