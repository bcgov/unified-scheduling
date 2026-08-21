using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Unified.Audit;
using Unified.Authorization.Claims;
using Xunit;

namespace Unified.Tests.Unified.Audit;

public class HttpContextActorResolverTests
{
    [Fact]
    public void Resolve_WithAuthenticatedUser_ReturnsActorFromClaims()
    {
        var userId = Guid.NewGuid();
        var claimsPrincipal = new ClaimsPrincipal(
            new ClaimsIdentity(
                [
                    new Claim(UnifiedClaimTypes.UserId, userId.ToString()),
                    new Claim(ClaimTypes.Name, "Casey Sheriff"),
                ],
                authenticationType: "Test"
            )
        );

        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal },
        };

        var resolver = new HttpContextActorResolver(accessor);

        var actor = resolver.Resolve();

        Assert.Equal(userId, actor.ActorUserId);
        Assert.Equal("Casey Sheriff", actor.ActorName);
    }

    [Fact]
    public void Resolve_WithoutUser_FallsBackToSystemActor()
    {
        var resolver = new HttpContextActorResolver(new HttpContextAccessor());

        var actor = resolver.Resolve();

        Assert.Equal(Guid.Empty, actor.ActorUserId);
        Assert.Equal("system", actor.ActorName);
    }
}
