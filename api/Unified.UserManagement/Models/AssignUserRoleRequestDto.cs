namespace Unified.UserManagement.Models;

public sealed record AssignUserRoleRequestDto
{
    public required int RoleId { get; init; }

    /// <summary>
    /// Date in yyyy-MM-dd format. Backend will convert to user's timezone start-of-day.
    /// </summary>
    public required string EffectiveDate { get; init; }

    /// <summary>
    /// Date in yyyy-MM-dd format. Backend will convert to user's timezone end-of-day.
    /// </summary>
    public string? ExpiryDate { get; init; }
}
