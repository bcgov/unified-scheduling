using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using IdentityModel.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Unified.Infrastructure.Helpers;
using Unified.Infrastructure.Options;
namespace Unified.Infrastructure;

{
    public static class AuthenticationServiceCollectionExtension
{
    public static IServiceCollection AddUnifiedAuthentication(
        this IServiceCollection services,
        IWebHostEnvironment env,
        IConfiguration configuration
    )
    {
        services.AddHttpClient();

        var keycloakOptions = services
            .BuildServiceProvider()
            .GetRequiredService<IOptions<KeycloakOptions>>()
            .Value;

        var refreshThreshold = KeycloakOptions.DefaultRefreshThreshold;
        if (!string.IsNullOrWhiteSpace(keycloakOptions.RefreshThreshold))
        {
            TimeSpan.TryParse(keycloakOptions.RefreshThreshold, out refreshThreshold);
        }

        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultAuthenticateScheme =
                    CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.Cookie.Name = "ProbateAuth";
                if (env.IsDevelopment())
                    options.Cookie.Name += ".Development";

                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.None;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

                options.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToAccessDenied = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return context.Response.CompleteAsync();
                    },
                    OnValidatePrincipal = async cookieCtx =>
                    {
                        var accessTokenExpiration = DateTimeOffset.Parse(
                            cookieCtx.Properties.GetTokenValue("expires_at")
                                ?? DateTimeOffset.UtcNow.ToString()
                        );
                        var timeRemaining = accessTokenExpiration.Subtract(
                            DateTimeOffset.UtcNow
                        );

                        if (timeRemaining > refreshThreshold)
                            return;

                        var refreshToken = cookieCtx.Properties.GetTokenValue("refresh_token");
                        if (string.IsNullOrEmpty(refreshToken))
                            return;

                        var httpClientFactory =
                            cookieCtx.HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
                        var httpClient = httpClientFactory.CreateClient();

                        // Wrap in using to dispose the underlying HttpRequestMessage and prevent resource leaks
                        using (
                            var refreshRequest = new RefreshTokenRequest
                            {
                                Address =
                                    keycloakOptions.Authority
                                    + "/protocol/openid-connect/token",
                                ClientId = keycloakOptions.Client,
                                ClientSecret = keycloakOptions.Secret,
                                RefreshToken = refreshToken,
                            }
                        )
                        {
                            var response = await httpClient.RequestRefreshTokenAsync(
                                refreshRequest
                            );

                            if (response.IsError)
                            {
                                cookieCtx.RejectPrincipal();
                                await cookieCtx.HttpContext.SignOutAsync(
                                    CookieAuthenticationDefaults.AuthenticationScheme
                                );
                            }
                            else
                            {
                                var expiresInSeconds = response.ExpiresIn;
                                var updatedExpiresAt = DateTimeOffset.UtcNow.AddSeconds(
                                    expiresInSeconds
                                );
                                cookieCtx.Properties.UpdateTokenValue(
                                    "expires_at",
                                    updatedExpiresAt.ToString()
                                );
                                cookieCtx.Properties.UpdateTokenValue(
                                    "refresh_token",
                                    response.RefreshToken
                                );
                                cookieCtx.ShouldRenew = true;
                            }
                        }
                    },
                };
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

                // Set explicit cookie paths so nginx's proxy_cookie_path directive
                options.CorrelationCookie.Path = "/";
                options.NonceCookie.Path = "/";

                // Disable Pushed Authorization Requests (PAR) so that kc_idp_hint
                // is sent as a query parameter on the authorization URL rather than
                options.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Disable;

                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");

                options.Events = new OpenIdConnectEvents
                {
                    OnTicketReceived = context =>
                    {
                        //id_token_hint for Keycloak logout.
                        context.Properties.UpdateTokenValue("access_token", null);
                        context.Properties.UpdateTokenValue("refresh_token", null);
                        context.Properties.Items[".TokenNames"] = "id_token";
                        return Task.CompletedTask;
                    },
                    OnRedirectToIdentityProvider = context =>
                    {
                        // Set the redirect URI explicitly using forwarded headers if available.
                        // X-Forwarded-Host should contain host:port when a non-standard
                        // port is needed (e.g. localhost:8080 for local dev).
                        // X-Forwarded-Port is NOT used here because on OpenShift the
                        // nginx proxy leaks the internal container port (8080) which
                        // is wrong for the external URL (standard 443).
                        var request = context.HttpContext.Request;
                        var forwardedHost = request
                            .Headers["X-Forwarded-Host"]
                            .FirstOrDefault();
                        var forwardedProto =
                            request.Headers["X-Forwarded-Proto"].FirstOrDefault()
                            ?? request.Scheme;

                        var baseHref = XForwardedForHelper.ResolveBaseHref(request);
                        var callbackPathWithBase =
                            $"{baseHref.TrimEnd('/')}{context.Options.CallbackPath}";

                        string redirectUri;
                        if (!string.IsNullOrEmpty(forwardedHost))
                        {
                            redirectUri =
                                $"{forwardedProto}://{forwardedHost}{callbackPathWithBase}";
                        }
                        else
                        {
                            // Fallback to request host (preserves port for local dev)
                            redirectUri =
                                $"{request.Scheme}://{request.Host}{callbackPathWithBase}";
                        }

                        context.ProtocolMessage.RedirectUri = redirectUri;

                        // Check if kc_idp_hint was set in authentication properties (from login endpoint)
                        if (
                            context.Properties.Items.TryGetValue("kc_idp_hint", out var idpHint)
                        )
                        {
                            context.ProtocolMessage.SetParameter("kc_idp_hint", idpHint);
                        }
                        else
                        {
                            // Fallback to configuration default
                            context.ProtocolMessage.SetParameter(
                                "kc_idp_hint",
                                keycloakOptions.KcIdpHint
                            );
                        }
                        return Task.CompletedTask;
                    },
                };
            });

        return services;
    }
}
}
