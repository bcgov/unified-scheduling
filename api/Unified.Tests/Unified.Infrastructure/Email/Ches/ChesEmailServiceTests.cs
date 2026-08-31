using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Unified.Core.Email;
using Unified.Infrastructure.Email.Ches;
using Unified.Infrastructure.Email.Ches.Generated;
using Unified.Tests.TestHelpers;

namespace Unified.Tests.Infrastructure.Email.Ches;

public sealed class ChesEmailServiceTests
{
    [Theory]
    [InlineData(EmailBodyType.Text, "text")]
    [InlineData(EmailBodyType.Html, "html")]
    public async Task SendAsync_ValidMessage_MapsRequestAndReturnsAcceptanceIdentifiers(
        EmailBodyType bodyType,
        string expectedBodyType
    )
    {
        var transactionId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var handler = new RecordingHttpMessageHandler((_, _) => CreateAcceptedResponse(transactionId, messageId));
        var logger = new RecordingLogger<ChesEmailService>();
        var service = CreateService(handler, logger);
        var correlationId = "schedule-distribution:123";
        var message = new EmailMessage
        {
            To = [" to@example.com "],
            Cc = ["cc@example.com"],
            Bcc = ["bcc@example.com"],
            Subject = "Sensitive subject",
            Body = "Sensitive body",
            BodyType = bodyType,
            UnifiedCorrelationId = correlationId,
            Attachments =
            [
                new EmailAttachment
                {
                    FileName = "report.pdf",
                    ContentType = "application/pdf",
                    Content = new MemoryStream([1, 2, 3, 4]),
                },
            ],
        };

        var result = await service.SendAsync(message, TestContext.Current.CancellationToken);

        Assert.Equal(transactionId.ToString("D"), result.TransactionId);
        Assert.Equal(messageId.ToString("D"), Assert.Single(result.Messages).MessageId);
        Assert.True(Guid.TryParse(result.Tag, out _));
        Assert.NotEqual(correlationId, result.Tag);
        Assert.Equal(1, handler.CallCount);

        using var requestJson = JsonDocument.Parse(Assert.Single(handler.RequestBodies));
        var root = requestJson.RootElement;
        Assert.Equal("\"Unified Scheduling\" <sender@example.com>", root.GetProperty("from").GetString());
        Assert.Equal(["to@example.com"], ReadStringArray(root.GetProperty("to")));
        Assert.Equal(["cc@example.com"], ReadStringArray(root.GetProperty("cc")));
        Assert.Equal(["bcc@example.com"], ReadStringArray(root.GetProperty("bcc")));
        Assert.Equal("Sensitive subject", root.GetProperty("subject").GetString());
        Assert.Equal("Sensitive body", root.GetProperty("body").GetString());
        Assert.Equal(expectedBodyType, root.GetProperty("bodyType").GetString());
        Assert.Equal("utf-8", root.GetProperty("encoding").GetString());
        Assert.Equal("normal", root.GetProperty("priority").GetString());
        Assert.Equal(0, root.GetProperty("delayTS").GetInt32());
        Assert.Equal(result.Tag, root.GetProperty("tag").GetString());

        var attachment = Assert.Single(root.GetProperty("attachments").EnumerateArray().ToArray());
        Assert.Equal("report.pdf", attachment.GetProperty("filename").GetString());
        Assert.Equal("application/pdf", attachment.GetProperty("contentType").GetString());
        Assert.Equal("base64", attachment.GetProperty("encoding").GetString());
        Assert.Equal(Convert.ToBase64String([1, 2, 3, 4]), attachment.GetProperty("content").GetString());
    }

    [Fact]
    public async Task SendAsync_TwoSubmissions_GeneratesDifferentChesTags()
    {
        var handler = new RecordingHttpMessageHandler((_, _) => CreateAcceptedResponse(Guid.NewGuid(), Guid.NewGuid()));
        var service = CreateService(handler);
        var message = CreateMessage();

        var first = await service.SendAsync(message, TestContext.Current.CancellationToken);
        var second = await service.SendAsync(message, TestContext.Current.CancellationToken);

        Assert.NotEqual(first.Tag, second.Tag);
        Assert.Equal(2, handler.CallCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task SendAsync_ExplicitChesFailure_ThrowsDefiniteDeliveryException(HttpStatusCode statusCode)
    {
        var handler = new RecordingHttpMessageHandler(
            (_, _) =>
                new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json"),
                }
        );
        var service = CreateService(handler);

        var exception = await Assert.ThrowsAsync<EmailDeliveryException>(() =>
            service.SendAsync(CreateMessage(), TestContext.Current.CancellationToken)
        );

        Assert.Equal((int)statusCode, exception.StatusCode);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task SendAsync_InvalidAcceptedResponse_ThrowsStateUnknownWithoutGeneratedResponseContent()
    {
        const string sensitiveResponse = "recipient@example.com sensitive response content";
        var handler = new RecordingHttpMessageHandler(
            (_, _) =>
                new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent(sensitiveResponse, Encoding.UTF8, "application/json"),
                }
        );
        var service = CreateService(handler);

        var exception = await Assert.ThrowsAsync<EmailDeliveryStateUnknownException>(() =>
            service.SendAsync(CreateMessage(), TestContext.Current.CancellationToken)
        );

        Assert.DoesNotContain(sensitiveResponse, exception.ToString(), StringComparison.Ordinal);
        Assert.IsType<ChesAcceptedResponseException>(exception.InnerException);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task SendAsync_TransportFailureAfterPostDispatch_DoesNotRetryAndThrowsStateUnknown()
    {
        var transport = new ThrowingHttpMessageHandler(new HttpRequestException("connection dropped"));
        var authenticationHandler = new ChesAuthenticationHandler(
            new StubAccessTokenProvider(),
            Options.Create(CreateOptions())
        )
        {
            InnerHandler = transport,
        };
        var service = CreateService(authenticationHandler);

        var exception = await Assert.ThrowsAsync<EmailDeliveryStateUnknownException>(() =>
            service.SendAsync(CreateMessage(), TestContext.Current.CancellationToken)
        );

        Assert.Equal(1, transport.CallCount);
        Assert.IsType<ChesPostOutcomeUnknownException>(exception.InnerException);
    }

    [Fact]
    public async Task SendAsync_SuccessLog_DoesNotContainSensitiveMessageData()
    {
        const string recipient = "private-recipient@example.com";
        const string subject = "private-subject-value";
        const string body = "private-body-value";
        var handler = new RecordingHttpMessageHandler((_, _) => CreateAcceptedResponse(Guid.NewGuid(), Guid.NewGuid()));
        var logger = new RecordingLogger<ChesEmailService>();
        var service = CreateService(handler, logger);
        var message = CreateMessage() with
        {
            To = [recipient],
            Subject = subject,
            Body = body,
            Attachments =
            [
                new EmailAttachment
                {
                    FileName = "private-filename.pdf",
                    ContentType = "application/pdf",
                    Content = new MemoryStream(Encoding.UTF8.GetBytes("private-attachment-content")),
                },
            ],
        };

        await service.SendAsync(message, TestContext.Current.CancellationToken);

        var logText = string.Join(Environment.NewLine, logger.Entries.Select(entry => entry.Message));
        Assert.DoesNotContain(recipient, logText, StringComparison.Ordinal);
        Assert.DoesNotContain(subject, logText, StringComparison.Ordinal);
        Assert.DoesNotContain(body, logText, StringComparison.Ordinal);
        Assert.DoesNotContain("private-filename.pdf", logText, StringComparison.Ordinal);
        Assert.DoesNotContain("private-attachment-content", logText, StringComparison.Ordinal);
    }

    [Fact]
    public void EmailMessageContract_DoesNotExposeCallerControlledSenderOrChesTag()
    {
        var propertyNames = typeof(EmailMessage).GetProperties().Select(property => property.Name).ToArray();

        Assert.DoesNotContain("From", propertyNames);
        Assert.DoesNotContain("SenderEmail", propertyNames);
        Assert.DoesNotContain("SenderName", propertyNames);
        Assert.DoesNotContain("Tag", propertyNames);
    }

    private static ChesEmailService CreateService(
        HttpMessageHandler handler,
        RecordingLogger<ChesEmailService>? logger = null
    )
    {
        var options = CreateOptions();
        var client = new HttpClient(handler) { BaseAddress = new Uri(options.BaseUrl) };
        return new ChesEmailService(
            new ChesClient(client),
            new EmailMessageValidator(),
            new ChesEmailMessagePreparer(Options.Create(options)),
            Options.Create(options),
            logger ?? new RecordingLogger<ChesEmailService>()
        );
    }

    private static ChesOptions CreateOptions() =>
        new()
        {
            BaseUrl = "https://ches.example.com/api/v1/",
            AuthUrl = "https://auth.example.com/token",
            ClientId = "client-id",
            ClientSecret = "client-secret",
            SenderName = "Unified Scheduling",
            SenderEmail = "sender@example.com",
            TimeoutSeconds = 30,
            MaxRecipientsPerMessage = 500,
            MaxAttachmentSizeBytes = 1024,
            AllowedAttachmentTypes =
            [
                new ChesAttachmentTypeOptions { Extension = ".pdf", ContentType = "application/pdf" },
            ],
        };

    private static EmailMessage CreateMessage() =>
        new()
        {
            To = ["recipient@example.com"],
            Subject = "Subject",
            Body = "Body",
            UnifiedCorrelationId = "test-correlation",
        };

    private static HttpResponseMessage CreateAcceptedResponse(Guid transactionId, Guid messageId) =>
        new(HttpStatusCode.Created)
        {
            Content = new StringContent(
                $$"""
                {
                  "messages": [
                    {
                      "msgId": "{{messageId:D}}",
                      "tag": "provider-tag",
                      "to": ["recipient@example.com"]
                    }
                  ],
                  "txId": "{{transactionId:D}}"
                }
                """,
                Encoding.UTF8,
                "application/json"
            ),
        };

    private static string[] ReadStringArray(JsonElement element) =>
        element.EnumerateArray().Select(item => item.GetString()!).ToArray();

    private sealed class RecordingHttpMessageHandler(Func<HttpRequestMessage, int, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            CallCount++;
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return responseFactory(request, CallCount);
        }
    }

    private sealed class ThrowingHttpMessageHandler(Exception exception) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            CallCount++;
            return Task.FromException<HttpResponseMessage>(exception);
        }
    }

    private sealed class StubAccessTokenProvider : IChesAccessTokenProvider
    {
        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken) => Task.FromResult("access-token");

        public void Invalidate(string rejectedAccessToken) { }
    }
}
