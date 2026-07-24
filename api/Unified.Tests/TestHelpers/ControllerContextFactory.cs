using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.Authorization.Claims;

namespace Unified.Tests.TestHelpers;

/// <summary>
/// Helper for building a <see cref="ControllerContext"/> with a synthetic <see cref="ClaimsPrincipal"/>
/// for unit testing controllers that use <c>User.FindFirst</c> or <c>User.HasClaim</c>.
/// </summary>
internal static class ControllerContextFactory
{
    /// <summary>
    /// Creates a <see cref="ControllerContext"/> whose <c>User</c> contains the given UserId claim
    /// and optionally a set of permission claims.
    /// Pass <c>null</c> for <paramref name="userId"/> to simulate a missing / unrecognised user.
    /// </summary>
    public static ControllerContext CreateWithUserId(Guid? userId, IEnumerable<string>? permissions = null)
    {
        var claims = new List<Claim>();

        if (userId.HasValue)
            claims.Add(new Claim(UnifiedClaimTypes.UserId, userId.Value.ToString()));

        foreach (var permission in permissions ?? [])
            claims.Add(new Claim(UnifiedClaimTypes.Permission, permission));

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        return new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } };
    }
}
