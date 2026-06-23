using System.Security.Claims;

namespace Unified.Authorization.Claims;

public static class ClaimPrincipalExtensions
{
    public static int HomeLocationId(this ClaimsPrincipal user)
    {
        var homeLocationIdString = user.FindFirst(UnifiedClaimTypes.HomeLocationId)?.Value;
        var parsed = int.TryParse(homeLocationIdString, out var homeLocationId);
        return parsed ? homeLocationId : -5000;
    }

    public static string HomeLocationTimezone(this ClaimsPrincipal user) =>
        user.FindFirst(UnifiedClaimTypes.HomeLocationTimezone)?.Value ?? string.Empty;

    public static Guid CurrentUserId(this ClaimsPrincipal user)
    {
        var userIdString = user.FindFirst(UnifiedClaimTypes.UserId)?.Value;
        if (!Guid.TryParse(userIdString, out var userId))
            throw new InvalidOperationException("Missing UserId Guid from claims.");

        return userId;
    }
}
