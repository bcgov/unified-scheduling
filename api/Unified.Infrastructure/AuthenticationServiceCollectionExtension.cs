using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Unified.Infrastructure.Helpers;
using Unified.Infrastructure.Options;

namespace Unified.Infrastructure;

public static class AuthenticationServiceCollectionExtension
{
    private const string IdirIdClaimType = "keycloak/idir_user_guid";
    private static readonly TimeSpan TransientRefreshFailureCooldown = TimeSpan.FromSeconds(10);

    private sealed class TokenRefreshCooldown(TimeProvider timeProvider)
    {
        private long retryAfterUtcTicks;

        public bool IsActive => timeProvider.GetUtcNow().UtcTicks < Interlocked.Read(ref retryAfterUtcTicks);

        public void Start() =>
            Interlocked.Exchange(
                ref retryAfterUtcTicks,
                timeProvider.GetUtcNow().Add(TransientRefreshFailureCooldown).UtcTicks
            );

        public void Reset() => Interlocked.Exchange(ref retryAfterUtcTicks, 0);
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("id_token")] string? IdToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn
    );

    private static async Task ValidateAndRefreshPrincipalAsync(
        CookieValidatePrincipalContext cookieCtx,
        KeycloakOptions keycloakOptions
    )
    {
        if (cookieCtx.Principal is null)
        {
            cookieCtx.RejectPrincipal();
            await cookieCtx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        var externalSubject =
            cookieCtx.Principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? cookieCtx.Principal.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(externalSubject))
        {
            cookieCtx.RejectPrincipal();
            await cookieCtx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        var expiresAt = cookieCtx.Properties.GetTokenValue("expires_at");
        if (string.IsNullOrWhiteSpace(expiresAt) || !DateTimeOffset.TryParse(expiresAt, out var accessTokenExpiration))
        {
            cookieCtx.RejectPrincipal();
            await cookieCtx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        var timeProvider = cookieCtx.HttpContext.RequestServices.GetRequiredService<TimeProvider>();
        var cooldown = cookieCtx.HttpContext.RequestServices.GetRequiredService<TokenRefreshCooldown>();
        var now = timeProvider.GetUtcNow();
        if (accessTokenExpiration - now > keycloakOptions.TokenRefreshThreshold)
            return;

        var refreshToken = cookieCtx.Properties.GetTokenValue("refresh_token");
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            cookieCtx.RejectPrincipal();
            await cookieCtx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        void RejectIfExpired()
        {
            if (accessTokenExpiration <= timeProvider.GetUtcNow())
                cookieCtx.RejectPrincipal();
        }

        if (cooldown.IsActive && accessTokenExpiration > timeProvider.GetUtcNow())
        {
            return;
        }

        var logger = cookieCtx
            .HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(AuthenticationServiceCollectionExtension));
        var httpClient = cookieCtx
            .HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>()
            .CreateClient("TokenRefresh");
        var tokenEndpoint = string.IsNullOrWhiteSpace(keycloakOptions.RefreshTokenEndpoint)
            ? $"{keycloakOptions.Authority.TrimEnd('/')}/protocol/openid-connect/token"
            : $"{keycloakOptions.Authority.TrimEnd('/')}/{keycloakOptions.RefreshTokenEndpoint.TrimStart('/')}";

        HttpResponseMessage response;
        try
        {
            using var content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["client_id"] = keycloakOptions.Client,
                    ["client_secret"] = keycloakOptions.Secret,
                    ["refresh_token"] = refreshToken,
                }
            );
            response = await httpClient.PostAsync(tokenEndpoint, content, cookieCtx.HttpContext.RequestAborted);
        }
        catch (OperationCanceledException) when (!cookieCtx.HttpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogWarning("Keycloak token refresh timed out.");
            cooldown.Start();
            RejectIfExpired();
            return;
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Keycloak token refresh could not reach the identity provider.");
            cooldown.Start();
            RejectIfExpired();
            return;
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                if (
                    response.StatusCode
                    is HttpStatusCode.BadRequest
                        or HttpStatusCode.Unauthorized
                        or HttpStatusCode.Forbidden
                )
                {
                    cookieCtx.RejectPrincipal();
                }
                else
                {
                    logger.LogWarning(
                        "Keycloak token refresh returned transient HTTP status {StatusCode}.",
                        (int)response.StatusCode
                    );
                    if (
                        response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
                        || (int)response.StatusCode >= 500
                    )
                        cooldown.Start();
                    RejectIfExpired();
                }
                return;
            }

            TokenResponse? tokenResponse;
            try
            {
                tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(
                    cookieCtx.HttpContext.RequestAborted
                );
            }
            catch (Exception exception)
                when (exception is HttpRequestException or System.Text.Json.JsonException or NotSupportedException)
            {
                logger.LogWarning(exception, "Keycloak token refresh returned an invalid response.");
                RejectIfExpired();
                return;
            }

            if (
                tokenResponse is null
                || string.IsNullOrWhiteSpace(tokenResponse.AccessToken)
                || tokenResponse.ExpiresIn <= 0
            )
            {
                logger.LogWarning("Keycloak token refresh returned an incomplete response.");
                RejectIfExpired();
                return;
            }

            var replacementRefreshToken = string.IsNullOrWhiteSpace(tokenResponse.RefreshToken)
                ? refreshToken
                : tokenResponse.RefreshToken;
            var existingIdToken = cookieCtx.Properties.GetTokenValue("id_token");
            var replacementIdToken = string.IsNullOrWhiteSpace(tokenResponse.IdToken)
                ? existingIdToken
                : tokenResponse.IdToken;
            var refreshedTokens = new List<AuthenticationToken>
            {
                new() { Name = "refresh_token", Value = replacementRefreshToken },
                new()
                {
                    Name = "expires_at",
                    Value = timeProvider.GetUtcNow().AddSeconds(tokenResponse.ExpiresIn).ToString("O"),
                },
            };
            if (!string.IsNullOrWhiteSpace(replacementIdToken))
                refreshedTokens.Add(new AuthenticationToken { Name = "id_token", Value = replacementIdToken });

            cookieCtx.Properties.StoreTokens(refreshedTokens);
            cookieCtx.ShouldRenew = true;
            cooldown.Reset();
        }
    }

    // Claims read from the cookie principal: UnifiedClaimsTransformer and cookie validation
    // (NameIdentifier/sub), UserAccountResolutionService (IDIR GUID, NameIdentifier, idir_username),
    // and the audit actor / user-info display name (Name). Application claims are added per request.
    private static readonly string[] RetainedClaimTypes =
    [
        ClaimTypes.NameIdentifier,
        "sub",
        IdirIdClaimType,
        "idir_user_guid",
        "idir_username",
        ClaimTypes.Name,
        "name",
    ];

    /// <summary>
    /// Rebuilds the Keycloak principal with only the claims the application reads, to keep the
    /// authentication cookie small. Claims are recreated without per-claim issuer metadata.
    /// </summary>
    private static ClaimsPrincipal? CreateCompactPrincipal(ClaimsPrincipal? principal)
    {
        if (principal?.Identity is not ClaimsIdentity identity)
            return principal;

        var retainedTypes = new HashSet<string>(RetainedClaimTypes, StringComparer.Ordinal) { identity.NameClaimType };
        var claims = principal
            .Claims.Where(claim => retainedTypes.Contains(claim.Type) && !string.IsNullOrEmpty(claim.Value))
            .Select(claim => (claim.Type, claim.Value))
            .Distinct()
            .Select(claim => new Claim(claim.Type, claim.Value));

        return new ClaimsPrincipal(
            new ClaimsIdentity(claims, identity.AuthenticationType, identity.NameClaimType, identity.RoleClaimType)
        );
    }

    /// <summary>
    /// Development only: logs the authentication cookie size so ticket growth is visible.
    /// Logs sizes only, never cookie, token, or claim values.
    /// </summary>
    private static Task LogAuthenticationCookieSize(CookieSigningInContext context)
    {
        if (context.Principal is null)
            return Task.CompletedTask;

        var ticket = new AuthenticationTicket(context.Principal, context.Properties, context.Scheme.Name);
        var serializedBytes = TicketSerializer.Default.Serialize(ticket).Length;
        var protectedLength = context.Options.TicketDataFormat.Protect(ticket).Length;

        // Mirrors ChunkingCookieManager: one cookie when it fits, otherwise chunks sized after the attribute template.
        var chunkSize = (context.Options.CookieManager as ChunkingCookieManager)?.ChunkSize;
        var templateLength = context
            .CookieOptions.CreateCookieHeader(context.Options.Cookie.Name ?? string.Empty, string.Empty)
            .ToString()
            .Length;
        var chunkCount =
            chunkSize is not int size || size > templateLength + protectedLength
                ? 1
                : (int)Math.Ceiling(protectedLength / (double)Math.Max(1, size - templateLength - 3));

        context
            .HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(AuthenticationServiceCollectionExtension))
            .LogInformation(
                "Authentication cookie size (Development): serialized ticket {SerializedTicketBytes} bytes, protected value {ProtectedValueLength} characters, estimated {CookieChunkCount} cookie(s).",
                serializedBytes,
                protectedLength,
                chunkCount
            );
        return Task.CompletedTask;
    }

    public static IServiceCollection AddUnifiedAuthentication(
        this IServiceCollection services,
        IHostEnvironment env,
        IConfiguration configuration
    )
    {
        var keycloakOptions = configuration.GetSection(KeycloakOptions.SectionName).Get<KeycloakOptions>();

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<TokenRefreshCooldown>();

        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.Cookie.Name = keycloakOptions.CookieName;
                if (env.IsDevelopment())
                    options.Cookie.Name += ".Development";

                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.None;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                // Scope the cookie (and its chunks and sign-out deletes) to the public base path.
                options.Cookie = new BaseHrefCookieBuilder(options.Cookie);

                options.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToAccessDenied = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return context.Response.CompleteAsync();
                    },
                    OnValidatePrincipal = ctx => ValidateAndRefreshPrincipalAsync(ctx, keycloakOptions),
                };
                if (env.IsDevelopment())
                    options.Events.OnSigningIn = LogAuthenticationCookieSize;
            })
            .AddOpenIdConnect(options =>
            {
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.Authority = keycloakOptions.Authority;
                options.ClientId = keycloakOptions.Client;
                options.ClientSecret = keycloakOptions.Secret;
                options.RequireHttpsMetadata = !env.IsDevelopment();
                options.GetClaimsFromUserInfoEndpoint = true;
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.UsePkce = true;
                options.SaveTokens = true;
                options.CallbackPath = "/api/auth/signin-oidc";
                options.SignedOutCallbackPath = "/api/auth/signout-callback-oidc";

                // Scope the correlation and nonce cookies to the public base path, keeping their defaults.
                options.CorrelationCookie = new BaseHrefCookieBuilder(options.CorrelationCookie);
                options.NonceCookie = new BaseHrefCookieBuilder(options.NonceCookie);

                // Disable Pushed Authorization Requests (PAR) so that kc_idp_hint
                // is sent as a query parameter on the authorization URL rather than
                options.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Disable;

                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.ClaimActions.MapJsonKey(IdirIdClaimType, "idir_user_guid");
                options.ClaimActions.MapJsonKey("idir_username", "idir_username");

                options.Events = new OpenIdConnectEvents
                {
                    OnTicketReceived = context =>
                    {
                        var retainedTokens = context
                            .Properties.GetTokens()
                            .Where(token =>
                                (token.Name is "refresh_token" or "id_token" or "expires_at")
                                && !string.IsNullOrWhiteSpace(token.Value)
                            )
                            .ToArray();
                        context.Properties.StoreTokens(retainedTokens);
                        context.Principal = CreateCompactPrincipal(context.Principal);
                        return Task.CompletedTask;
                    },
                    OnRedirectToIdentityProvider = context =>
                    {
                        var request = context.HttpContext.Request;
                        var baseHref = XForwardedForHelper.ResolveBaseHref(request);

                        context.ProtocolMessage.RedirectUri = XForwardedForHelper.BuildUrlString(
                            forwardedProto: request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? request.Scheme,
                            forwardedHost: request.Headers["X-Forwarded-Host"].FirstOrDefault()
                                ?? request.Host.ToString(),
                            forwardedPort: request.Headers["X-Forwarded-Port"].FirstOrDefault() ?? "",
                            baseUrl: baseHref,
                            remainingPath: context.Options.CallbackPath
                        );
                        // Check if kc_idp_hint was set in authentication properties (from login endpoint)
                        if (context.Properties.Items.TryGetValue("kc_idp_hint", out var idpHint))
                        {
                            context.ProtocolMessage.SetParameter("kc_idp_hint", idpHint);
                        }
                        else
                        {
                            // Fallback to configuration default
                            context.ProtocolMessage.SetParameter("kc_idp_hint", keycloakOptions.KcIdpHint);
                        }
                        return Task.CompletedTask;
                    },
                    OnRedirectToIdentityProviderForSignOut = context =>
                    {
                        var request = context.HttpContext.Request;
                        var baseHref = XForwardedForHelper.ResolveBaseHref(request);

                        // Keep the final landing URL in protected OIDC state. Keycloak must
                        // return through the middleware callback before this redirect is used.
                        var applicationUrl = XForwardedForHelper.BuildUrlString(
                            forwardedProto: request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? request.Scheme,
                            forwardedHost: request.Headers["X-Forwarded-Host"].FirstOrDefault()
                                ?? request.Host.ToString(),
                            forwardedPort: request.Headers["X-Forwarded-Port"].FirstOrDefault() ?? "",
                            baseUrl: baseHref,
                            remainingPath: "/"
                        );
                        context.Properties.RedirectUri = applicationUrl + "logged-out";
                        context.ProtocolMessage.PostLogoutRedirectUri =
                            applicationUrl + context.Options.SignedOutCallbackPath.Value!.TrimStart('/');

                        return Task.CompletedTask;
                    },
                    OnSignedOutCallbackRedirect = async context =>
                    {
                        var logger = context
                            .HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                            .CreateLogger(nameof(AuthenticationServiceCollectionExtension));
                        context.HandleResponse();
                        if (
                            context.Properties is null
                            || string.IsNullOrWhiteSpace(context.Properties.RedirectUri)
                            || !string.IsNullOrEmpty(context.ProtocolMessage?.Error)
                        )
                        {
                            logger.LogWarning("OIDC logout callback was invalid or reported a provider error.");
                            await Results
                                .Problem(
                                    statusCode: StatusCodes.Status502BadGateway,
                                    title: "Sign-out could not be completed.",
                                    detail: "The identity-provider session may still be active."
                                )
                                .ExecuteAsync(context.HttpContext);
                            return;
                        }

                        if (
                            !Uri.TryCreate(keycloakOptions.SiteMinderLogoutUrl, UriKind.Absolute, out var logoutUri)
                            || logoutUri.Scheme != Uri.UriSchemeHttps
                        )
                        {
                            logger.LogError("The upstream SiteMinder logout URL is missing or invalid.");
                            await Results
                                .Problem(
                                    statusCode: StatusCodes.Status503ServiceUnavailable,
                                    title: "Sign-out could not be completed.",
                                    detail: "Upstream logout is not configured. The identity-provider session may still be active."
                                )
                                .ExecuteAsync(context.HttpContext);
                            return;
                        }

                        // Keycloak has completed logout. Clear the upstream browser session
                        // before SiteMinder returns to the public logged-out page.
                        context.Response.Redirect(
                            QueryHelpers.AddQueryString(
                                logoutUri.AbsoluteUri,
                                new Dictionary<string, string?>
                                {
                                    ["returl"] = context.Properties.RedirectUri,
                                    ["retnow"] = "1",
                                }
                            )
                        );
                    },
                };
            });

        return services;
    }
}
