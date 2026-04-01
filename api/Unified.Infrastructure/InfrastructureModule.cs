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
using Unified.FeatureFlags;
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
        services.AddSingleton<
            IValidateOptions<FeatureFlags.FeatureFlags>,
            RequiredBooleanOptionsValidator<FeatureFlags.FeatureFlags>
        >();

        services
            .AddOptions<KeycloakOptions>()
            .BindConfiguration(KeycloakOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<FeatureFlags.FeatureFlags>()
            .BindConfiguration(FeatureFlags.FeatureFlags.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IFeatureFlags, FeatureFlagsAccessor>();

        services.AddHttpClient("TokenRefresh");
        services.AddHttpContextAccessor();

        var sp = services.BuildServiceProvider();
        var env = sp.GetRequiredService<IHostEnvironment>();
        var configuration = sp.GetRequiredService<IConfiguration>();

        services.AddUnifiedAuthentication(env, configuration);

        return services;
    }
}
