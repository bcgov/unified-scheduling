namespace Unified.UserManagement.Models;

public sealed record ExpireActingPositionRequestDto
{
    public required int ActingPositionId { get; init; }

    public required string ExpiryReason { get; init; }
}
