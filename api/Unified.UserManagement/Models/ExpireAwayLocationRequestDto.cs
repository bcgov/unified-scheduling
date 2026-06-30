namespace Unified.UserManagement.Models;

public sealed record ExpireAwayLocationRequestDto
{
    public required int AwayLocationId { get; init; }

    public required string ExpiryReason { get; init; }
}
