namespace Unified.UserManagement.Models;

public sealed record DeleteRoleWithReassignmentRequestDto
{
    /// <summary>
    /// The ID of the role to reassign users to.
    /// Required only when the role has active user assignments.
    /// </summary>
    public int? NewRoleId { get; init; }

    /// <summary>
    /// The effective date for the new role assignment.
    /// Required only when the role has active user assignments.
    /// Expected format: yyyy-MM-dd (e.g. "2025-12-31")
    /// </summary>
    public string? NewRoleEffectiveDate { get; init; }

    /// <summary>
    /// The expiry date for the new role assignment (optional).
    /// Expected format: yyyy-MM-dd (e.g. "2025-12-31")
    /// </summary>
    public string? NewRoleExpiryDate { get; init; }
}
