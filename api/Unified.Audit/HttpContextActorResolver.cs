using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Unified.Authorization.Claims;
using Unified.Db.Models.UserManagement;

namespace Unified.Audit;

public sealed class HttpContextActorResolver(IHttpContextAccessor httpContextAccessor) : ICurrentActorResolver
{
    public CurrentActor Resolve()
    {
        var user = httpContextAccessor.HttpContext?.User;
        var userId = user?.TryGetCurrentUserId();

        return userId.HasValue
            ? new CurrentActor(userId, ResolveActorName(user))
            : new CurrentActor(User.SystemUser, "System");
    }

    private static string ResolveActorName(ClaimsPrincipal? user)
    {
        var fullName = user?.FindFirst(ClaimTypes.Name)?.Value;
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName;
        }

        var firstName = user?.FindFirst(UnifiedClaimTypes.FirstName)?.Value;
        var lastName = user?.FindFirst(UnifiedClaimTypes.LastName)?.Value;
        var joinedName = string.Join(
            " ",
            new[] { firstName, lastName }.Where(value => !string.IsNullOrWhiteSpace(value))
        );

        return string.IsNullOrWhiteSpace(joinedName) ? "System" : joinedName;
    }
}
