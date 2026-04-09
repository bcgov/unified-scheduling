using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
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

public static class AuthenticationServiceCollectionExtension
{
    private record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn
    );

    public static IServiceCollection AddUnifiedAuthentication(
        this IServiceCollection services,
        IHostEnvironment env,
        IConfiguration configuration
    )
    {
        var keycloakOptions = configuration.GetSection(KeycloakOptions.SectionName).Get<KeycloakOptions>();

        var refreshThreshold = KeycloakOptions.DefaultRefreshThreshold;
        if (!string.IsNullOrWhiteSpace(keycloakOptions.RefreshThreshold))
        {
            TimeSpan.TryParse(keycloakOptions.RefreshThreshold, out refreshThreshold);
        }

        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.Cookie.Name = keycloakOptions.CookieName;
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
                        var expiresAt = cookieCtx.Properties.GetTokenValue("expires_at");
                        if (
                            string.IsNullOrWhiteSpace(expiresAt)
                            || !DateTimeOffset.TryParse(expiresAt, out var accessTokenExpiration)
                        )
                        {
                            cookieCtx.RejectPrincipal();
                            await cookieCtx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                            return;
                        }

                        var timeRemaining = accessTokenExpiration.Subtract(DateTimeOffset.UtcNow);
                        if (timeRemaining > refreshThreshold)
                            return;

                        var refreshToken = cookieCtx.Properties.GetTokenValue("refresh_token");
                        if (string.IsNullOrWhiteSpace(refreshToken))
                            return;

                        var httpClientFactory =
                            cookieCtx.HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
                        var httpClient = httpClientFactory.CreateClient("TokenRefresh");

                        var tokenEndpoint = $"{keycloakOptions.Authority}/protocol/openid-connect/token";
                        var content = new FormUrlEncodedContent(
                            new Dictionary<string, string>
                            {
                                ["grant_type"] = "refresh_token",
                                ["client_id"] = keycloakOptions.Client,
                                ["client_secret"] = keycloakOptions.Secret,
                                ["refresh_token"] = refreshToken,
                            }
                        );

                        var response = await httpClient.PostAsync(tokenEndpoint, content);
                        if (!response.IsSuccessStatusCode)
                        {
                            cookieCtx.RejectPrincipal();
                            await cookieCtx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        }
                        else
                        {
                            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
                            if (tokenResponse != null)
                            {
                                var updatedExpiresAt = DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                                cookieCtx.Properties.UpdateTokenValue("expires_at", updatedExpiresAt.ToString());
                                cookieCtx.Properties.UpdateTokenValue("access_token", tokenResponse.AccessToken);
                                cookieCtx.Properties.UpdateTokenValue("refresh_token", tokenResponse.RefreshToken);
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
                };
            });

        return services;
    }
}
