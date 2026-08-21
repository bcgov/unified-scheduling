using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Unified.Api.Controllers;
using Unified.Api.Models;
using Unified.Api.Validators;
using Unified.Core.Email;

namespace Unified.Tests.Api.Controllers;

public sealed class ChesTestControllerTests
{
    private const string ExpectedSubject = "Unified Scheduling CHES Test";
    private const string ExpectedBody =
        "This is a test email sent from the Unified Scheduling local development environment.";

    [Fact]
    public void Registration_WhenDevelopmentAndChesEnabled_MapsTestEndpoint()
    {
        using var provider = CreateStartupLikeProvider(Environments.Development, chesEnabled: true);

        var actions = GetChesTestActions(provider);

        var action = Assert.Single(actions);
        Assert.Equal("api/dev/email/test", action.AttributeRouteInfo?.Template);
        Assert.NotNull(provider.GetService<TestEmailRequestValidator>());
    }

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Staging", true)]
    [InlineData("Development", false)]
    public void Registration_WhenEnvironmentOrChesConfigurationDisallowsEndpoint_DoesNotMapTestEndpoint(
        string environmentName,
        bool chesEnabled
    )
    {
        using var provider = CreateStartupLikeProvider(environmentName, chesEnabled);

        var actions = GetChesTestActions(provider);

        Assert.Empty(actions);
        Assert.Null(provider.GetService<TestEmailRequestValidator>());
    }

    [Fact]
    public void ControllerAuthorization_RequiresAuthenticationWithoutExplicitPolicy()
    {
        var attribute = Assert.Single(typeof(ChesTestController).GetCustomAttributes<AuthorizeAttribute>());

        Assert.Null(attribute.Policy);
        Assert.Null(attribute.Roles);
        Assert.Null(attribute.AuthenticationSchemes);
    }

    [Fact]
    public async Task SendTestEmail_ValidRequest_UsesSharedServiceWithFixedContentAndReturnsIdentifiers()
    {
        var transactionId = Guid.NewGuid().ToString("D");
        var tag = Guid.NewGuid().ToString("D");
        var messageIds = new[] { Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D") };
        var emailService = new RecordingEmailService
        {
            Result = new EmailSendResult
            {
                TransactionId = transactionId,
                Tag = tag,
                Messages = messageIds.Select(id => new EmailMessageSendResult { MessageId = id }).ToArray(),
            },
        };
        var controller = new ChesTestController(emailService, new TestEmailRequestValidator());
        var cancellationToken = TestContext.Current.CancellationToken;

        var result = await controller.SendTestEmail(
            new TestEmailRequest { Recipient = "recipient@example.com" },
            cancellationToken
        );

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<TestEmailResponse>(okResult.Value);
        Assert.Equal(transactionId, response.TransactionId);
        Assert.Equal(tag, response.Tag);
        Assert.Equal(messageIds, response.MessageIds);

        var message = Assert.IsType<EmailMessage>(emailService.Message);
        Assert.Equal(["recipient@example.com"], message.To);
        Assert.Empty(message.Cc);
        Assert.Empty(message.Bcc);
        Assert.Equal(ExpectedSubject, message.Subject);
        Assert.Equal(ExpectedBody, message.Body);
        Assert.Equal(EmailBodyType.Text, message.BodyType);
        Assert.Empty(message.Attachments);
        Assert.StartsWith("local-ches-test:", message.UnifiedCorrelationId, StringComparison.Ordinal);
        Assert.True(Guid.TryParse(message.UnifiedCorrelationId!["local-ches-test:".Length..], out _));
        Assert.Equal(cancellationToken, emailService.CancellationToken);
        Assert.Equal(1, emailService.CallCount);
    }

    [Fact]
    public async Task SendTestEmail_EmailServiceFails_PropagatesExceptionUnchanged()
    {
        var expectedException = new EmailDeliveryException(
            Guid.NewGuid().ToString("D"),
            "local-ches-test:test",
            recipientCount: 1,
            attachmentCount: 0,
            statusCode: 500
        );
        var emailService = new RecordingEmailService { Exception = expectedException };
        var controller = new ChesTestController(emailService, new TestEmailRequestValidator());

        var exception = await Assert.ThrowsAsync<EmailDeliveryException>(() =>
            controller.SendTestEmail(
                new TestEmailRequest { Recipient = "recipient@example.com" },
                TestContext.Current.CancellationToken
            )
        );

        Assert.Same(expectedException, exception);
    }

    private static ServiceProvider CreateStartupLikeProvider(string environmentName, bool chesEnabled)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Ches:Enabled"] = chesEnabled.ToString() })
            .Build();
        var environment = new FakeHostEnvironment { EnvironmentName = environmentName };
        var services = new ServiceCollection();
        services.AddLogging();

        var mvcBuilder = services.AddControllers().AddApplicationPart(typeof(ChesTestController).Assembly);
        mvcBuilder.AddChesTestController(environment, configuration);

        return services.BuildServiceProvider();
    }

    private static ControllerActionDescriptor[] GetChesTestActions(IServiceProvider provider) =>
        provider
            .GetRequiredService<IActionDescriptorCollectionProvider>()
            .ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
            .Where(action => action.ControllerTypeInfo.AsType() == typeof(ChesTestController))
            .ToArray();

    private sealed class RecordingEmailService : IEmailService
    {
        public EmailSendResult Result { get; init; } =
            new() { TransactionId = Guid.NewGuid().ToString("D"), Tag = Guid.NewGuid().ToString("D") };

        public Exception? Exception { get; init; }

        public EmailMessage? Message { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public int CallCount { get; private set; }

        public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            CallCount++;
            Message = message;
            CancellationToken = cancellationToken;

            return Exception is null ? Task.FromResult(Result) : Task.FromException<EmailSendResult>(Exception);
        }
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = nameof(ChesTestControllerTests);

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
