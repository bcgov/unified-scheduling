using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Unified.FeatureFlags;
using Unified.Infrastructure.ErrorHandling;
using Unified.Infrastructure.Hangfire;
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
        services.AddProblemDetails();

        services.AddExceptionHandler<GlobalExceptionHandler>();

        // Form-reading failures (e.g. multipart body length limit exceeded from
        // RequestFormLimitsFromOptionsAttribute) are captured by MVC as a generic ModelState
        // error message (no Exception instance is attached). Detect that specific message and
        // rethrow as an InvalidDataException so it flows through the standard exception-handling
        // pipeline and is handled by GlobalExceptionHandler.
        services.Configure<ApiBehaviorOptions>(options =>
        {
            var defaultFactory = options.InvalidModelStateResponseFactory;

            options.InvalidModelStateResponseFactory = context =>
            {
                var formReadError = context
                    .ModelState.Values.SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault(m => m.StartsWith("Failed to read the request form.", StringComparison.Ordinal));

                if (formReadError is not null)
                {
                    throw new InvalidDataException(formReadError);
                }

                return defaultFactory(context);
            };
        });

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

        // Background job processing (Hangfire) - a no-op if DatabaseConnectionString isn't
        // configured, so safe to call unconditionally here alongside the other core services.
        services.AddHangfireModule(configuration);

        return services;
    }
}
