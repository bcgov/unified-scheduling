using System.Security.Claims;

namespace Unified.Authorization.Claims;

public static class ClaimPrincipalExtensions
{
    public static Guid CurrentUserId(this ClaimsPrincipal user)
    {
        var userIdString = user.FindFirst(UnifiedClaimTypes.UserId)?.Value;
        if (!Guid.TryParse(userIdString, out var userId))
            throw new InvalidOperationException("Missing UserId Guid from claims.");

        return userId;
    }
}
