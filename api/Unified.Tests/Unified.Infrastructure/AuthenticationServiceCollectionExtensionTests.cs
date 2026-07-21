using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Unified.Authorization;
using Unified.Authorization.Claims;
using Unified.Infrastructure;
using Unified.Infrastructure.Options;

namespace Unified.Tests.Infrastructure;

public class AuthenticationServiceCollectionExtensionTests
{
    [Fact]
    public void AddUnifiedAuthentication_Should_Map_IdirClaims_From_UserInfo()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"{KeycloakOptions.SectionName}:Authority"] = "https://keycloak.example.com/realms/test",
                    [$"{KeycloakOptions.SectionName}:Client"] = "unified-client",
                    [$"{KeycloakOptions.SectionName}:Secret"] = "test-secret",
                    [$"{KeycloakOptions.SectionName}:Audience"] = "unified-api",
                    [$"{KeycloakOptions.SectionName}:CookieName"] = "UnifiedAuth",
                }
            )
            .Build();
        var environment = new FakeHostEnvironment { EnvironmentName = Environments.Development };

        // Act
        services.AddUnifiedAuthentication(environment, configuration);
        using var serviceProvider = services.BuildServiceProvider();
        var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>();
        var options = optionsMonitor.Get(OpenIdConnectDefaults.AuthenticationScheme);
        var idirUserGuidAction = options
            .ClaimActions.OfType<JsonKeyClaimAction>()
            .SingleOrDefault(action =>
                action.ClaimType == UnifiedClaimTypes.IdirId && action.JsonKey == "idir_user_guid"
            );
        var idirUsernameAction = options
            .ClaimActions.OfType<JsonKeyClaimAction>()
            .SingleOrDefault(action => action.ClaimType == "idir_username" && action.JsonKey == "idir_username");

        // Assert
        Assert.True(options.GetClaimsFromUserInfoEndpoint);
        Assert.NotNull(idirUserGuidAction);
        Assert.NotNull(idirUsernameAction);
    }

    [Fact]
    public async Task CookieValidation_Should_Reject_Principal_Without_External_Subject()
    {
        // Arrange
        var options = CreateCookieOptions();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "Test User")], "Cookies"));
        var properties = CreateAuthenticationPropertiesWithFutureTokenExpiration();
        var context = CreateCookieValidatePrincipalContext(options, principal, properties);

        // Act
        await options.Events.OnValidatePrincipal(context);

        // Assert
        Assert.Null(context.Principal);
    }

    private static CookieAuthenticationOptions CreateCookieOptions()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"{KeycloakOptions.SectionName}:Authority"] = "https://keycloak.example.com/realms/test",
                    [$"{KeycloakOptions.SectionName}:Client"] = "unified-client",
                    [$"{KeycloakOptions.SectionName}:Secret"] = "test-secret",
                    [$"{KeycloakOptions.SectionName}:Audience"] = "unified-api",
                    [$"{KeycloakOptions.SectionName}:CookieName"] = "UnifiedAuth",
                }
            )
            .Build();
        var environment = new FakeHostEnvironment { EnvironmentName = Environments.Development };

        services.AddUnifiedAuthentication(environment, configuration);
        using var serviceProvider = services.BuildServiceProvider();
        var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();

        return optionsMonitor.Get(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    private static AuthenticationProperties CreateAuthenticationPropertiesWithFutureTokenExpiration()
    {
        var properties = new AuthenticationProperties();
        properties.StoreTokens([
            new AuthenticationToken { Name = "expires_at", Value = DateTimeOffset.UtcNow.AddHours(1).ToString("O") },
        ]);

        return properties;
    }

    private static CookieValidatePrincipalContext CreateCookieValidatePrincipalContext(
        CookieAuthenticationOptions options,
        ClaimsPrincipal principal,
        AuthenticationProperties properties
    )
    {
        var scheme = new AuthenticationScheme(
            CookieAuthenticationDefaults.AuthenticationScheme,
            CookieAuthenticationDefaults.AuthenticationScheme,
            typeof(CookieAuthenticationHandler)
        );
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddLogging()
                .AddSingleton<IAuthenticationService, NoopAuthenticationService>()
                .BuildServiceProvider(),
        };
        var ticket = new AuthenticationTicket(principal, properties, CookieAuthenticationDefaults.AuthenticationScheme);

        return new CookieValidatePrincipalContext(context, scheme, options, ticket);
    }

    private sealed class NoopAuthenticationService : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }

        public Task SignInAsync(
            HttpContext context,
            string? scheme,
            ClaimsPrincipal principal,
            AuthenticationProperties? properties
        )
        {
            return Task.CompletedTask;
        }

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = nameof(AuthenticationServiceCollectionExtensionTests);

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
