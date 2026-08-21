using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Unified.Core;
using Unified.Core.Email;
using Unified.Infrastructure.Email.Ches;
using Unified.Infrastructure.Email.Ches.Generated;

namespace Unified.Tests.Infrastructure.Email.Ches;

public sealed class ChesEmailServiceCollectionExtensionsTests
{
    [Fact]
    public void AddChesEmail_WhenEnabled_RegistersCohesiveServiceGraphAndBindsOptions()
    {
        var configuration = CreateConfiguration(enabled: true);
        var services = new ServiceCollection();

        services.AddSingleton(configuration);
        services.AddChesEmail(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IEmailService)
                && descriptor.ImplementationType == typeof(ChesEmailService)
                && descriptor.Lifetime == ServiceLifetime.Transient
        );
        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IChesAccessTokenProvider)
                && descriptor.ImplementationType == typeof(ChesAccessTokenProvider)
                && descriptor.Lifetime == ServiceLifetime.Singleton
        );
        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(ChesEmailMessagePreparer)
                && descriptor.Lifetime == ServiceLifetime.Singleton
        );
        Assert.Equal("sender@example.com", provider.GetRequiredService<IOptions<ChesOptions>>().Value.SenderEmail);
    }

    [Fact]
    public void AddChesEmail_WhenDisabled_DoesNotRegisterEmailProviderServices()
    {
        var configuration = CreateConfiguration(enabled: false);
        var services = new ServiceCollection();

        services.AddSingleton(configuration);
        services.AddChesEmail(configuration);

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IEmailService));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IChesAccessTokenProvider));
        Assert.False(ChesEmailConfiguration.IsEnabled(configuration));
    }

    [Fact]
    public async Task ConfiguredHttpPipeline_PostEmailReceivesTransientError_DoesNotRetrySubmission()
    {
        var configuration = CreateConfiguration(enabled: true);
        var transport = new CountingHttpMessageHandler();
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddCoreModule();
        services.AddChesEmail(configuration);
        services.RemoveAll<IChesAccessTokenProvider>();
        services.AddSingleton<IChesAccessTokenProvider, StubAccessTokenProvider>();
        services.AddHttpClient<ChesClient>().ConfigurePrimaryHttpMessageHandler(() => transport);
        using var provider = services.BuildServiceProvider();
        var emailService = provider.GetRequiredService<IEmailService>();
        var message = new EmailMessage
        {
            To = ["recipient@example.com"],
            Subject = "Subject",
            Body = "Body",
        };

        var exception = await Assert.ThrowsAsync<EmailDeliveryException>(() =>
            emailService.SendAsync(message, TestContext.Current.CancellationToken)
        );

        Assert.Equal((int)HttpStatusCode.InternalServerError, exception.StatusCode);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task ConfiguredHttpPipeline_PostEmailTransportFails_DoesNotRetrySubmission()
    {
        var configuration = CreateConfiguration(enabled: true);
        var transport = new TransportFailureHttpMessageHandler();
        var services = CreateServices(configuration, transport);
        using var provider = services.BuildServiceProvider();
        var emailService = provider.GetRequiredService<IEmailService>();
        var message = new EmailMessage
        {
            To = ["recipient@example.com"],
            Subject = "Subject",
            Body = "Body",
        };

        await Assert.ThrowsAsync<EmailDeliveryStateUnknownException>(() =>
            emailService.SendAsync(message, TestContext.Current.CancellationToken)
        );

        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task ConfiguredHttpPipeline_GetHealthReceivesTransientError_RetriesSafeRequest()
    {
        var configuration = CreateConfiguration(enabled: true);
        var transport = new SafeGetRetryHttpMessageHandler();
        var services = CreateServices(configuration, transport);
        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ChesClient>();

        var response = await client.GetHealthAsync(TestContext.Current.CancellationToken);

        Assert.Empty(response.Dependencies);
        Assert.Equal(2, transport.CallCount);
    }

    private static ServiceCollection CreateServices(IConfiguration configuration, HttpMessageHandler transport)
    {
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddCoreModule();
        services.AddChesEmail(configuration);
        services.RemoveAll<IChesAccessTokenProvider>();
        services.AddSingleton<IChesAccessTokenProvider, StubAccessTokenProvider>();
        services.AddHttpClient<ChesClient>().ConfigurePrimaryHttpMessageHandler(() => transport);
        return services;
    }

    private static IConfiguration CreateConfiguration(bool enabled) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Ches:Enabled"] = enabled.ToString(),
                    ["Ches:BaseUrl"] = "https://ches.example.com/api/v1/",
                    ["Ches:AuthUrl"] = "https://auth.example.com/token",
                    ["Ches:ClientId"] = "client-id",
                    ["Ches:ClientSecret"] = "client-secret",
                    ["Ches:SenderName"] = "Unified Scheduling",
                    ["Ches:SenderEmail"] = "sender@example.com",
                    ["Ches:TimeoutSeconds"] = "30",
                    ["Ches:TokenRefreshSkewSeconds"] = "60",
                    ["Ches:MaxAttachmentSizeBytes"] = "1024",
                    ["Ches:MaxRecipientsPerMessage"] = "500",
                    ["Ches:AllowedAttachmentTypes:0:Extension"] = ".pdf",
                    ["Ches:AllowedAttachmentTypes:0:ContentType"] = "application/pdf",
                }
            )
            .Build();

    private sealed class CountingHttpMessageHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            CallCount++;
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json"),
                }
            );
        }
    }

    private sealed class TransportFailureHttpMessageHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            CallCount++;
            throw new HttpRequestException("connection dropped");
        }
    }

    private sealed class SafeGetRetryHttpMessageHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            CallCount++;
            Assert.Equal(HttpMethod.Get, request.Method);

            return Task.FromResult(
                CallCount == 1
                    ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    : new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"dependencies\":[]}", Encoding.UTF8, "application/json"),
                    }
            );
        }
    }

    private sealed class StubAccessTokenProvider : IChesAccessTokenProvider
    {
        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken) => Task.FromResult("access-token");

        public void Invalidate(string rejectedAccessToken) { }
    }
}
