using System.Net;
using Microsoft.Extensions.Options;
using Unified.Infrastructure.Email.Ches;

namespace Unified.Tests.Infrastructure.Email.Ches;

public sealed class ChesAuthenticationHandlerTests
{
    [Fact]
    public async Task SendAsync_ChesRejectsCachedToken_InvalidatesTokenWithoutRetryingPost()
    {
        var tokenProvider = new RecordingAccessTokenProvider();
        var transport = new UnauthorizedHttpMessageHandler();
        var handler = new ChesAuthenticationHandler(
            tokenProvider,
            Options.Create(new ChesOptions { TimeoutSeconds = 30 })
        )
        {
            InnerHandler = transport,
        };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://ches.example.com/api/v1/") };

        using var response = await client.PostAsync(
            "email",
            new StringContent("{}"),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("cached-token", tokenProvider.InvalidatedToken);
        Assert.Equal(1, tokenProvider.GetTokenCallCount);
        Assert.Equal(1, transport.CallCount);
    }

    private sealed class RecordingAccessTokenProvider : IChesAccessTokenProvider
    {
        public int GetTokenCallCount { get; private set; }

        public string? InvalidatedToken { get; private set; }

        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            GetTokenCallCount++;
            return Task.FromResult("cached-token");
        }

        public void Invalidate(string rejectedAccessToken)
        {
            InvalidatedToken = rejectedAccessToken;
        }
    }

    private sealed class UnauthorizedHttpMessageHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            CallCount++;
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("cached-token", request.Headers.Authorization?.Parameter);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        }
    }
}
