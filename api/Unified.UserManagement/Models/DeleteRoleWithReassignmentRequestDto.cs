namespace Unified.UserManagement.Models;

/// <summary>
/// Request model for deleting a role and reassigning its users to another role.
/// </summary>
public sealed record DeleteRoleWithReassignmentRequestDto
{
    /// <summary>
    /// The ID of the role to reassign users to (optional if no users are assigned).
    /// </summary>
    public int NewRoleId { get; init; }

    /// <summary>
    /// The effective date for the new role assignment (required if NewRoleId is provided).
    /// ISO 8601 format: "2025-12-31T00:00:00Z"
    /// </summary>
    public string NewRoleEffectiveDate { get; init; }

    /// <summary>
    /// The expiry date for the new role assignment (optional).
    /// ISO 8601 format: "2025-12-31T00:00:00Z"
    /// </summary>
    public string? NewRoleExpiryDate { get; init; }
}
