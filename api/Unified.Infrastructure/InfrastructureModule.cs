using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Unified.Infrastructure.Helpers;
using Unified.Infrastructure.Options;

namespace Unified.Infrastructure;

/// <summary>
/// Infrastructure module for dependency injection and configuration
/// </summary>
public static class InfrastructureModule
{
    /// <summary>
    /// Add infrastructure services including authorization and authentication
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddInfrastructureModule(this IServiceCollection services)
    {
        services
            .AddOptions<KeycloakOptions>()
            .BindConfiguration(KeycloakOptions.ConfigurationSection)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient("TokenRefresh");
        services.AddHttpContextAccessor();

        AddAuthorizationAndAuthentication(services);

        return services;
    }

    /// <summary>
    /// Configure authentication and authorization with Keycloak
    /// </summary>
    private static void AddAuthorizationAndAuthentication(IServiceCollection services)
    {
        var serviceProvider = services.BuildServiceProvider();
        var keycloakOptions = serviceProvider.GetRequiredService<IOptions<KeycloakOptions>>().Value;
        var env = serviceProvider.GetRequiredService<IHostEnvironment>();

        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.Cookie.Name = "UnifiedAuthCookie";
                options.Cookie.HttpOnly = true;
                // Keep strict settings for non-development; allow local HTTP development.
                options.Cookie.SameSite = SameSiteMode.None;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                //This should prevent resending this cookie on every request.
                options.Cookie.Path = "/api/auth";
                options.Events = new CookieAuthenticationEvents
                {
                    // After the auth cookie has been validated, this event is called.
                    // In it we see if the access token is close to expiring.  If it is
                    // then we use the refresh token to get a new access token and save them.
                    // If the refresh token does not work for some reason then we redirect to
                    // the login screen.
                    OnValidatePrincipal = async cookieCtx =>
                    {
                        var expiresAt = cookieCtx.Properties.GetTokenValue("expires_at");
                        if (string.IsNullOrWhiteSpace(expiresAt)
                            || !DateTimeOffset.TryParse(expiresAt, out var accessTokenExpiration))
                        {
                            cookieCtx.RejectPrincipal();
                            await cookieCtx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                            return;
                        }

                        var timeRemaining = accessTokenExpiration.Subtract(DateTimeOffset.UtcNow);
                        if (timeRemaining > keycloakOptions.TokenRefreshThreshold)
                            return;

                        var refreshToken = cookieCtx.Properties.GetTokenValue("refresh_token");
                        if (string.IsNullOrWhiteSpace(refreshToken))
                        {
                            cookieCtx.RejectPrincipal();
                            await cookieCtx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                            return;
                        }

                        var httpClientFactory =
                            cookieCtx.HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
                        var httpClient = httpClientFactory.CreateClient("TokenRefresh");

                        var tokenEndpoint = $"{keycloakOptions.Authority}{keycloakOptions.RefreshTokenEndpoint}";
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

                                // Indicate to the cookie middleware that the cookie should be remade (since we have updated it)
                                cookieCtx.ShouldRenew = true;
                            }
                        }
                    },
                };
            })
            .AddOpenIdConnect(
                OpenIdConnectDefaults.AuthenticationScheme,
                options =>
                {
                    options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.Authority = keycloakOptions.Authority;
                    options.ClientId = keycloakOptions.Client;
                    options.ClientSecret = keycloakOptions.Secret;
                    options.RequireHttpsMetadata = !env.IsDevelopment();
                    options.ResponseType = OpenIdConnectResponseType.Code;
                    options.UsePkce = true;
                    options.SaveTokens = true;

                    options.CallbackPath = keycloakOptions.CallbackPath;

                    options.Events = new OpenIdConnectEvents
                    {
                        OnTicketReceived = context =>
                        {
                            if (context.Properties?.Items != null)
                            {
                                context.Properties.Items.Remove(".Token.id_token");
                                context.Properties.Items[".TokenNames"] =
                                    "access_token;refresh_token;token_type;expires_at";
                            }
                            return Task.CompletedTask;
                        },
                        OnTokenValidated = context =>
                        {
                            if (context.Principal?.Identity is ClaimsIdentity identity)
                            {
                                var claimsToRemove = identity
                                    .Claims.Where(c =>
                                        c.Type != ClaimTypes.Name
                                        && c.Type != ClaimTypes.NameIdentifier
                                        && c.Type != "preferred_username"
                                    )
                                    .ToList();
                                foreach (var claim in claimsToRemove)
                                    identity.RemoveClaim(claim);
                            }
                            return Task.CompletedTask;
                        },
                        OnRedirectToIdentityProvider = context =>
                        {
                            var forwardedProto = context.HttpContext.Request.Headers["X-Forwarded-Proto"].ToString();
                            var forwardedHost = context.HttpContext.Request.Headers["X-Forwarded-Host"].ToString();
                            var forwardedPort = context.HttpContext.Request.Headers["X-Forwarded-Port"].ToString();

                            if (!string.IsNullOrWhiteSpace(forwardedHost))
                            {
                                context.ProtocolMessage.RedirectUri = XForwardedForHelper.BuildUrlString(
                                    forwardedProto,
                                    forwardedHost,
                                    forwardedPort,
                                    keycloakOptions.RedirectBaseUrl ?? "/",
                                    options.CallbackPath
                                );
                            }
                            else if (!string.IsNullOrEmpty(keycloakOptions.RedirectBaseUrl))
                            {
                                context.ProtocolMessage.RedirectUri =
                                    $"{keycloakOptions.RedirectBaseUrl.TrimEnd('/')}{keycloakOptions.CallbackPath}";
                            }

                            if (!string.IsNullOrEmpty(keycloakOptions.IdpHint))
                                context.ProtocolMessage.SetParameter("kc_idp_hint", keycloakOptions.IdpHint);
                            return Task.CompletedTask;
                        },
                    };
                }
            )
            .AddJwtBearer(
                JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    options.Authority = keycloakOptions.Authority;
                    options.Audience = keycloakOptions.Audience;
                    options.RequireHttpsMetadata = !env.IsDevelopment() ;
                    options.SaveToken = true;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ClockSkew = TimeSpan.FromSeconds(5),
                    };
                    options.Events = new JwtBearerEvents
                    {
                        OnAuthenticationFailed = context =>
                        {
                            context.NoResult();
                            context.Response.StatusCode = 401;
                            return Task.CompletedTask;
                        },
                        OnForbidden = context =>
                        {
                            context.NoResult();
                            context.Response.StatusCode = 403;
                            return Task.CompletedTask;
                        },
                    };
                    JsonWebTokenHandler.DefaultInboundClaimTypeMap.Remove("sub");
                }
            );

        services.AddAuthorization();
    }

    /// <summary>
    /// Token response model for refresh token flow
    /// </summary>
    private record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn
    );
}
