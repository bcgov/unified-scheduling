using System.Linq;
using System.Net;
using System.Net.Http.Json;
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
using Unified.Authorization.Claims;
using Unified.Infrastructure;
using Unified.Infrastructure.Options;

namespace Unified.Tests.Infrastructure;

public class AuthenticationServiceCollectionExtensionTests
{
    [Fact]
    public void AddUnifiedAuthentication_Should_Map_IdirClaims_From_UserInfo()
    {
        using var provider = CreateServiceProvider();
        var options = provider
            .GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(OpenIdConnectDefaults.AuthenticationScheme);

        Assert.True(options.GetClaimsFromUserInfoEndpoint);
        Assert.Contains(
            options.ClaimActions.OfType<JsonKeyClaimAction>(),
            action => action.ClaimType == UnifiedClaimTypes.IdirId && action.JsonKey == "idir_user_guid"
        );
        Assert.Contains(
            options.ClaimActions.OfType<JsonKeyClaimAction>(),
            action => action.ClaimType == "idir_username" && action.JsonKey == "idir_username"
        );
    }

    [Fact]
    public void AddUnifiedAuthentication_Should_Use_Cookie_As_Default_Challenge()
    {
        using var provider = CreateServiceProvider();
        var options = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme, options.DefaultChallengeScheme);
    }

    [Fact]
    public void KeycloakOptions_Should_Default_To_One_Minute_Refresh_Threshold()
    {
        Assert.Equal(TimeSpan.FromMinutes(1), new KeycloakOptions().TokenRefreshThreshold);
    }

    [Fact]
    public void KeycloakOptions_Should_Accept_Positive_Configured_Refresh_Threshold()
    {
        using var provider = CreateInfrastructureServiceProvider("00:02:00");

        var options = provider.GetRequiredService<IOptions<KeycloakOptions>>().Value;

        Assert.Equal(TimeSpan.FromMinutes(2), options.TokenRefreshThreshold);
    }

    [Fact]
    public void KeycloakOptions_Should_Reject_Negative_Configured_Refresh_Threshold()
    {
        using var provider = CreateInfrastructureServiceProvider("-00:00:01");

        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<KeycloakOptions>>().Value
        );

        Assert.Contains(nameof(KeycloakOptions.TokenRefreshThreshold), exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-date")]
    public async Task CookieValidation_Invalid_Expiration_Should_Reject_And_Sign_Out(string? expiresAt)
    {
        var (options, authenticationService) = CreateCookieOptions();
        var context = CreateContext(options, CreatePrincipal(), CreateProperties(expiresAt), authenticationService);

        await options.Events.OnValidatePrincipal(context);

        Assert.Null(context.Principal);
        Assert.Equal(1, authenticationService.SignOutCallCount);
    }

    [Fact]
    public async Task CookieValidation_Missing_External_Subject_Should_Reject_And_Sign_Out()
    {
        var (options, authenticationService) = CreateCookieOptions();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "Test User")], "Cookies"));
        var context = CreateContext(
            options,
            principal,
            CreateProperties(DateTimeOffset.UtcNow.AddHours(1).ToString("O")),
            authenticationService
        );

        await options.Events.OnValidatePrincipal(context);

        Assert.Null(context.Principal);
        Assert.Equal(1, authenticationService.SignOutCallCount);
    }

    [Fact]
    public async Task CookieValidation_Outside_Refresh_Window_Should_Not_Call_Keycloak()
    {
        var (options, authenticationService) = CreateCookieOptions();
        var handler = new RecordingHttpMessageHandler(_ => SuccessResponse());
        var principal = CreatePrincipal();
        var properties = CreateProperties(
            DateTimeOffset.UtcNow.AddMinutes(5).ToString("O"),
            refreshToken: "refresh",
            idToken: "id"
        );
        var context = CreateContext(options, principal, properties, authenticationService, handler);

        await options.Events.OnValidatePrincipal(context);
        await options.Events.OnValidatePrincipal(context);

        Assert.Same(principal, context.Principal);
        Assert.Equal(0, handler.CallCount);
        Assert.False(context.ShouldRenew);
    }

    [Fact]
    public async Task CookieValidation_Within_Refresh_Window_Without_RefreshToken_Should_Reject_And_Sign_Out()
    {
        var (options, authenticationService) = CreateCookieOptions();
        var context = CreateContext(
            options,
            CreatePrincipal(),
            CreateProperties(DateTimeOffset.UtcNow.AddSeconds(30).ToString("O")),
            authenticationService
        );

        await options.Events.OnValidatePrincipal(context);

        Assert.Null(context.Principal);
        Assert.Equal(1, authenticationService.SignOutCallCount);
    }

    [Theory]
    [InlineData(-30)]
    [InlineData(30)]
    public async Task CookieValidation_RefreshSuccess_Should_Renew_Valid_Principal(int expirationOffsetSeconds)
    {
        var timeProvider = new MutableTimeProvider(new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero));
        var (options, authenticationService) = CreateCookieOptions();
        var handler = new RecordingHttpMessageHandler(_ => SuccessResponse("rotated-refresh", "rotated-id"));
        var permission = new Claim(UnifiedClaimTypes.Permission, "ShiftsView");
        var principal = CreatePrincipal(permission);
        var context = CreateContext(
            options,
            principal,
            CreateProperties(
                timeProvider.GetUtcNow().AddSeconds(expirationOffsetSeconds).ToString("O"),
                refreshToken: "original-refresh",
                idToken: "original-id",
                accessToken: "must-be-removed"
            ),
            authenticationService,
            handler,
            timeProvider
        );

        await options.Events.OnValidatePrincipal(context);
        await options.Events.OnValidatePrincipal(context);

        Assert.Same(principal, context.Principal);
        Assert.True(context.ShouldRenew);
        Assert.Equal(1, handler.CallCount);
        Assert.Equal("rotated-refresh", context.Properties.GetTokenValue("refresh_token"));
        Assert.Equal("rotated-id", context.Properties.GetTokenValue("id_token"));
        Assert.Null(context.Properties.GetTokenValue("access_token"));
        Assert.Equal(3, context.Properties.GetTokens().Count());
        Assert.Equal(
            timeProvider.GetUtcNow().AddSeconds(300),
            DateTimeOffset.Parse(context.Properties.GetTokenValue("expires_at")!)
        );
        Assert.Contains(
            context.Principal!.Claims,
            claim => claim.Type == permission.Type && claim.Value == permission.Value
        );
        Assert.Equal(0, authenticationService.SignOutCallCount);
    }

    [Fact]
    public async Task CookieValidation_RefreshResponseWithoutReplacementTokens_Should_Preserve_ExistingTokens()
    {
        var (options, authenticationService) = CreateCookieOptions();
        var handler = new RecordingHttpMessageHandler(_ => SuccessResponse(refreshToken: null, idToken: null));
        var context = CreateContext(
            options,
            CreatePrincipal(),
            CreateProperties(
                DateTimeOffset.UtcNow.AddSeconds(30).ToString("O"),
                refreshToken: "original-refresh",
                idToken: "original-id"
            ),
            authenticationService,
            handler
        );

        await options.Events.OnValidatePrincipal(context);

        Assert.Equal("original-refresh", context.Properties!.GetTokenValue("refresh_token"));
        Assert.Equal("original-id", context.Properties.GetTokenValue("id_token"));
        Assert.True(context.ShouldRenew);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task CookieValidation_NonRecoverableRefreshFailure_Should_Reject_Without_Deleting_Cookie(
        HttpStatusCode status
    )
    {
        var (options, authenticationService) = CreateCookieOptions();
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(status));
        var context = CreateRefreshContext(options, authenticationService, handler, expirationOffsetSeconds: 30);

        await options.Events.OnValidatePrincipal(context);

        Assert.Null(context.Principal);
        Assert.Equal(0, authenticationService.SignOutCallCount);
    }

    [Fact]
    public async Task CookieValidation_FailureAfterTokenExpires_Should_Reject_Principal()
    {
        var timeProvider = new MutableTimeProvider(new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero));
        var (options, authenticationService) = CreateCookieOptions();
        var handler = new RecordingHttpMessageHandler(_ =>
        {
            timeProvider.Advance(TimeSpan.FromSeconds(2));
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        });
        var context = CreateRefreshContext(
            options,
            authenticationService,
            handler,
            expirationOffsetSeconds: 1,
            timeProvider: timeProvider
        );

        await options.Events.OnValidatePrincipal(context);

        Assert.Null(context.Principal);
    }

    [Fact]
    public async Task CookieValidation_TransientFailure_Should_Suppress_Retries_Until_Cooldown_Expires()
    {
        var timeProvider = new MutableTimeProvider(new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero));
        var (options, authenticationService) = CreateCookieOptions();
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var requestServices = CreateRequestServices(authenticationService, handler, timeProvider);

        var first = CreateRefreshContext(options, authenticationService, handler, 30, timeProvider, requestServices);
        await options.Events.OnValidatePrincipal(first);

        var duringCooldown = CreateRefreshContext(
            options,
            authenticationService,
            handler,
            30,
            timeProvider,
            requestServices
        );
        await options.Events.OnValidatePrincipal(duringCooldown);

        Assert.NotNull(duringCooldown.Principal);
        Assert.Equal(1, handler.CallCount);

        timeProvider.Advance(TimeSpan.FromSeconds(11));
        var afterCooldown = CreateRefreshContext(
            options,
            authenticationService,
            handler,
            30,
            timeProvider,
            requestServices
        );
        await options.Events.OnValidatePrincipal(afterCooldown);

        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task CookieValidation_ExpiredCredentialsDuringCooldown_Should_Bypass_Cooldown_And_Renew()
    {
        var timeProvider = new MutableTimeProvider(new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero));
        var (options, authenticationService) = CreateCookieOptions();
        var responses = new Queue<HttpResponseMessage>([
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            SuccessResponse(),
        ]);
        var handler = new RecordingHttpMessageHandler(_ => responses.Dequeue());
        using var requestServices = CreateRequestServices(authenticationService, handler, timeProvider);
        var first = CreateRefreshContext(options, authenticationService, handler, 30, timeProvider, requestServices);
        await options.Events.OnValidatePrincipal(first);

        var expired = CreateRefreshContext(options, authenticationService, handler, -1, timeProvider, requestServices);
        await options.Events.OnValidatePrincipal(expired);

        Assert.NotNull(expired.Principal);
        Assert.True(expired.ShouldRenew);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task CookieValidation_ExpiredCredentialsDuringCooldown_Should_Reject_When_Refresh_Fails()
    {
        var timeProvider = new MutableTimeProvider(new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero));
        var (options, authenticationService) = CreateCookieOptions();
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var requestServices = CreateRequestServices(authenticationService, handler, timeProvider);
        var first = CreateRefreshContext(options, authenticationService, handler, 30, timeProvider, requestServices);
        await options.Events.OnValidatePrincipal(first);

        var expired = CreateRefreshContext(options, authenticationService, handler, -1, timeProvider, requestServices);
        await options.Events.OnValidatePrincipal(expired);

        Assert.Null(expired.Principal);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task CookieValidation_InvalidRefreshToken_Should_Not_Start_TransientFailureCooldown()
    {
        var timeProvider = new MutableTimeProvider(new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero));
        var (options, authenticationService) = CreateCookieOptions();
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
        using var requestServices = CreateRequestServices(authenticationService, handler, timeProvider);

        var first = CreateRefreshContext(options, authenticationService, handler, 30, timeProvider, requestServices);
        await options.Events.OnValidatePrincipal(first);
        var second = CreateRefreshContext(options, authenticationService, handler, 30, timeProvider, requestServices);
        await options.Events.OnValidatePrincipal(second);

        Assert.Null(first.Principal);
        Assert.Null(second.Principal);
        Assert.Equal(2, handler.CallCount);
    }

    [Theory]
    [InlineData(30, true)]
    [InlineData(-30, false)]
    public async Task CookieValidation_TransientHttpFailure_Should_Only_Preserve_Unexpired_Session(
        int expirationOffsetSeconds,
        bool expectedAuthenticated
    )
    {
        var (options, authenticationService) = CreateCookieOptions();
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var context = CreateRefreshContext(options, authenticationService, handler, expirationOffsetSeconds);

        await options.Events.OnValidatePrincipal(context);

        Assert.Equal(expectedAuthenticated, context.Principal is not null);
        Assert.Equal(0, authenticationService.SignOutCallCount);
        Assert.False(context.ShouldRenew);
    }

    [Theory]
    [InlineData(30, true)]
    [InlineData(-30, false)]
    public async Task CookieValidation_NetworkFailure_Should_Only_Preserve_Unexpired_Session(
        int expirationOffsetSeconds,
        bool expectedAuthenticated
    )
    {
        var (options, authenticationService) = CreateCookieOptions();
        var handler = new RecordingHttpMessageHandler(_ => throw new HttpRequestException("Unavailable"));
        var context = CreateRefreshContext(options, authenticationService, handler, expirationOffsetSeconds);

        await options.Events.OnValidatePrincipal(context);

        Assert.Equal(expectedAuthenticated, context.Principal is not null);
        Assert.Equal(0, authenticationService.SignOutCallCount);
    }

    [Theory]
    [InlineData(30, true)]
    [InlineData(-30, false)]
    public async Task CookieValidation_Timeout_Should_Only_Preserve_Unexpired_Session(
        int expirationOffsetSeconds,
        bool expectedAuthenticated
    )
    {
        var (options, authenticationService) = CreateCookieOptions();
        var handler = new RecordingHttpMessageHandler(_ => throw new TaskCanceledException("Timed out"));
        var context = CreateRefreshContext(options, authenticationService, handler, expirationOffsetSeconds);

        await options.Events.OnValidatePrincipal(context);

        Assert.Equal(expectedAuthenticated, context.Principal is not null);
        Assert.Equal(0, authenticationService.SignOutCallCount);
    }

    [Theory]
    [InlineData(30, true)]
    [InlineData(-30, false)]
    public async Task CookieValidation_MalformedSuccessResponse_Should_Only_Preserve_Unexpired_Session(
        int expirationOffsetSeconds,
        bool expectedAuthenticated
    )
    {
        var (options, authenticationService) = CreateCookieOptions();
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not-json"),
        });
        var context = CreateRefreshContext(options, authenticationService, handler, expirationOffsetSeconds);

        await options.Events.OnValidatePrincipal(context);

        Assert.Equal(expectedAuthenticated, context.Principal is not null);
        Assert.False(context.ShouldRenew);
    }

    [Fact]
    public async Task TicketReceived_Should_Retain_Only_Refresh_Id_And_Expiration_Tokens()
    {
        using var provider = CreateServiceProvider();
        var options = provider
            .GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(OpenIdConnectDefaults.AuthenticationScheme);
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1).ToString("O");
        var properties = CreateProperties(expiresAt, "refresh", "id", "access");
        properties.StoreTokens([
            .. properties.GetTokens(),
            new AuthenticationToken { Name = "token_type", Value = "Bearer" },
            new AuthenticationToken { Name = "scope", Value = "openid profile" },
        ]);
        var context = new TicketReceivedContext(
            new DefaultHttpContext { RequestServices = provider },
            new AuthenticationScheme(OpenIdConnectDefaults.AuthenticationScheme, null, typeof(OpenIdConnectHandler)),
            options,
            new AuthenticationTicket(CreatePrincipal(), properties, OpenIdConnectDefaults.AuthenticationScheme)
        );

        await options.Events.OnTicketReceived(context);

        var retainedProperties = context.Properties!;
        Assert.Equal("refresh", retainedProperties.GetTokenValue("refresh_token"));
        Assert.Equal("id", retainedProperties.GetTokenValue("id_token"));
        Assert.Equal(expiresAt, retainedProperties.GetTokenValue("expires_at"));
        Assert.Null(retainedProperties.GetTokenValue("access_token"));
        Assert.Null(retainedProperties.GetTokenValue("token_type"));
        Assert.Equal(3, retainedProperties.GetTokens().Count());
    }

    internal static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddUnifiedAuthentication(new FakeHostEnvironment(), CreateConfiguration());
        return services.BuildServiceProvider();
    }

    private static ServiceProvider CreateInfrastructureServiceProvider(string tokenRefreshThreshold)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                CreateConfiguration()
                    .AsEnumerable()
                    .Where(pair => pair.Value is not null)
                    .ToDictionary(pair => pair.Key, pair => pair.Value)
                    .Append(
                        new KeyValuePair<string, string?>(
                            $"{KeycloakOptions.SectionName}:TokenRefreshThreshold",
                            tokenRefreshThreshold
                        )
                    )
            )
            .Build();
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton<IHostEnvironment>(new FakeHostEnvironment());
        services.AddInfrastructureModule();
        return services.BuildServiceProvider();
    }

    private static (CookieAuthenticationOptions, RecordingAuthenticationService) CreateCookieOptions()
    {
        using var provider = CreateServiceProvider();
        var options = provider
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        return (options, new RecordingAuthenticationService());
    }

    internal static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
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

    internal static ClaimsPrincipal CreatePrincipal(params Claim[] additionalClaims) =>
        new(
            new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "external-user"), .. additionalClaims],
                CookieAuthenticationDefaults.AuthenticationScheme
            )
        );

    internal static AuthenticationProperties CreateProperties(
        string? expiresAt,
        string? refreshToken = null,
        string? idToken = null,
        string? accessToken = null
    )
    {
        var properties = new AuthenticationProperties();
        var tokens = new List<AuthenticationToken>();
        AddToken(tokens, "expires_at", expiresAt);
        AddToken(tokens, "refresh_token", refreshToken);
        AddToken(tokens, "id_token", idToken);
        AddToken(tokens, "access_token", accessToken);
        properties.StoreTokens(tokens);
        return properties;
    }

    private static void AddToken(List<AuthenticationToken> tokens, string name, string? value)
    {
        if (value is not null)
            tokens.Add(new AuthenticationToken { Name = name, Value = value });
    }

    private static CookieValidatePrincipalContext CreateRefreshContext(
        CookieAuthenticationOptions options,
        RecordingAuthenticationService authenticationService,
        HttpMessageHandler handler,
        int expirationOffsetSeconds,
        TimeProvider? timeProvider = null,
        ServiceProvider? requestServices = null
    ) =>
        CreateContext(
            options,
            CreatePrincipal(),
            CreateProperties(
                (timeProvider ?? TimeProvider.System).GetUtcNow().AddSeconds(expirationOffsetSeconds).ToString("O"),
                refreshToken: "refresh",
                idToken: "id"
            ),
            authenticationService,
            handler,
            timeProvider,
            requestServices
        );

    private static CookieValidatePrincipalContext CreateContext(
        CookieAuthenticationOptions options,
        ClaimsPrincipal principal,
        AuthenticationProperties properties,
        RecordingAuthenticationService authenticationService,
        HttpMessageHandler? handler = null,
        TimeProvider? timeProvider = null,
        ServiceProvider? requestServices = null
    )
    {
        requestServices ??= CreateRequestServices(authenticationService, handler, timeProvider);
        var httpContext = new DefaultHttpContext { RequestServices = requestServices };
        var scheme = new AuthenticationScheme(
            CookieAuthenticationDefaults.AuthenticationScheme,
            null,
            typeof(CookieAuthenticationHandler)
        );
        var ticket = new AuthenticationTicket(principal, properties, CookieAuthenticationDefaults.AuthenticationScheme);
        return new CookieValidatePrincipalContext(httpContext, scheme, options, ticket);
    }

    private static ServiceProvider CreateRequestServices(
        RecordingAuthenticationService authenticationService,
        HttpMessageHandler? handler = null,
        TimeProvider? timeProvider = null
    )
    {
        var services = new ServiceCollection().AddLogging();
        if (timeProvider is not null)
            services.AddSingleton(timeProvider);
        services.AddUnifiedAuthentication(new FakeHostEnvironment(), CreateConfiguration());
        services.AddSingleton<IAuthenticationService>(authenticationService);
        if (handler is not null)
            services.AddSingleton<IHttpClientFactory>(new TestHttpClientFactory(handler));
        return services.BuildServiceProvider();
    }

    private static HttpResponseMessage SuccessResponse(string? refreshToken = "refresh", string? idToken = "id") =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(
                new
                {
                    access_token = "access-is-never-stored",
                    refresh_token = refreshToken,
                    id_token = idToken,
                    expires_in = 300,
                }
            ),
        };

    private sealed class RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            CallCount++;
            return Task.FromResult(responseFactory(request));
        }
    }

    private sealed class TestHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        private readonly HttpClient _client = new(handler, disposeHandler: false);

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class RecordingAuthenticationService : IAuthenticationService
    {
        public int SignOutCallCount { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task SignInAsync(
            HttpContext context,
            string? scheme,
            ClaimsPrincipal principal,
            AuthenticationProperties? properties
        ) => Task.CompletedTask;

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            SignOutCallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset currentUtcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => currentUtcNow;

        public void Advance(TimeSpan duration) => currentUtcNow = currentUtcNow.Add(duration);
    }

    internal sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = nameof(AuthenticationServiceCollectionExtensionTests);
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
