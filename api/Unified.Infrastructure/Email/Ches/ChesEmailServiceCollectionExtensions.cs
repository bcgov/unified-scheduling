using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using Unified.Core.Email;
using Unified.Infrastructure.Email.Ches.Generated;

namespace Unified.Infrastructure.Email.Ches;

public static class ChesEmailServiceCollectionExtensions
{
    public static IServiceCollection AddChesEmail(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(ChesOptions.SectionName);
        var configuredOptions = section.Get<ChesOptions>() ?? new ChesOptions();
        var optionsBuilder = services.AddOptions<ChesOptions>().BindConfiguration(ChesOptions.SectionName);

        if (!ChesEmailConfiguration.IsEnabled(configuration))
            return services;

        services.AddSingleton<IValidateOptions<ChesOptions>, ChesOptionsValidator>();
        optionsBuilder.ValidateOnStart();

        var timeout = TimeSpan.FromSeconds(Math.Max(1, configuredOptions.TimeoutSeconds));

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ChesEmailMessagePreparer>();
        services.AddSingleton<ChesTokenClient>();
        services.AddSingleton<IChesAccessTokenProvider, ChesAccessTokenProvider>();
        services.AddTransient<ChesAuthenticationHandler>();

        services.AddHttpClient(ChesTokenClient.HttpClientName, client => client.Timeout = Timeout.InfiniteTimeSpan);

        services
            .AddHttpClient<ChesClient>(
                (serviceProvider, client) =>
                {
                    var options = serviceProvider.GetRequiredService<IOptions<ChesOptions>>().Value;
                    client.BaseAddress = new Uri(EnsureTrailingSlash(options.BaseUrl));
                    client.Timeout = Timeout.InfiniteTimeSpan;
                }
            )
            .AddHttpMessageHandler<ChesAuthenticationHandler>()
            .AddResilienceHandler(
                "ches-api",
                builder =>
                {
                    var retryOptions = CreateRetryOptions();
                    retryOptions.DisableForUnsafeHttpMethods();
                    builder.AddRetry(retryOptions).AddTimeout(timeout);
                }
            );

        services.AddTransient<IEmailService, ChesEmailService>();

        return services;
    }

    private static HttpRetryStrategyOptions CreateRetryOptions() =>
        new()
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(200),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
        };

    private static string EnsureTrailingSlash(string value) => value.EndsWith('/') ? value : value + "/";
}
