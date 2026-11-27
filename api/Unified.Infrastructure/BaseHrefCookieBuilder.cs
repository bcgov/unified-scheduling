using Microsoft.AspNetCore.Http;
using Unified.Infrastructure.Helpers;

namespace Unified.Infrastructure;

/// <summary>
/// Scopes a cookie to the public application base path resolved from <c>X-Base-Href</c>
/// (for example <c>/sheriff-scheduling</c>) instead of <c>/</c>, so authentication cookies are not
/// sent to every application on the shared host.
/// </summary>
/// <remarks>
/// Wraps the builder it replaces so that its name, flags, and expiration rules (for example the
/// OIDC correlation and nonce lifetimes) are preserved. Only the path is changed.
/// The same builder is used when issuing and deleting a cookie, including every
/// <see cref="Microsoft.AspNetCore.Authentication.Cookies.ChunkingCookieManager"/> chunk.
/// </remarks>
public sealed class BaseHrefCookieBuilder(CookieBuilder inner) : CookieBuilder
{
    public override string? Name
    {
        get => inner.Name;
        set => inner.Name = value;
    }

    /// <summary>Ignored by <see cref="Build(HttpContext, DateTimeOffset)"/>; the base path is used.</summary>
    public override string? Path
    {
        get => inner.Path;
        set => inner.Path = value;
    }

    public override string? Domain
    {
        get => inner.Domain;
        set => inner.Domain = value;
    }

    public override bool HttpOnly
    {
        get => inner.HttpOnly;
        set => inner.HttpOnly = value;
    }

    public override SameSiteMode SameSite
    {
        get => inner.SameSite;
        set => inner.SameSite = value;
    }

    public override CookieSecurePolicy SecurePolicy
    {
        get => inner.SecurePolicy;
        set => inner.SecurePolicy = value;
    }

    public override TimeSpan? Expiration
    {
        get => inner.Expiration;
        set => inner.Expiration = value;
    }

    public override TimeSpan? MaxAge
    {
        get => inner.MaxAge;
        set => inner.MaxAge = value;
    }

    public override bool IsEssential
    {
        get => inner.IsEssential;
        set => inner.IsEssential = value;
    }

    public override CookieOptions Build(HttpContext context, DateTimeOffset expiresFrom)
    {
        var options = inner.Build(context, expiresFrom);
        options.Path = XForwardedForHelper.ResolveCookiePath(context.Request);
        foreach (var extension in Extensions)
            options.Extensions.Add(extension);
        return options;
    }
}
