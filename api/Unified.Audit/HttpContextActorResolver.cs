using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Unified.Authorization.Claims;
using Unified.Db.Models.UserManagement;

namespace Unified.Audit;

public sealed class HttpContextActorResolver(IHttpContextAccessor httpContextAccessor) : ICurrentActorResolver
{
    public CurrentActor Resolve()
    {
        var httpContext = httpContextAccessor.HttpContext;
        var userId = httpContext?.User.TryGetCurrentUserId();

        if (userId.HasValue)
        {
            return new CurrentActor(userId, ResolveActorName(httpContext!.User));
        }

        // No HttpContext means this save is happening outside a request (startup migration/seeding,
        // background job, etc.) - attribute it to the system user rather than throwing.
        if (httpContext is null)
        {
            return new CurrentActor(User.SystemUser, "System");
        }

        throw new InvalidOperationException("Unable to resolve current actor: request has no authenticated user.");
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
