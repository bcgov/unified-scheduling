using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Timeout;
using Unified.Infrastructure.Email.Ches;
using Unified.Tests.TestHelpers;

namespace Unified.Tests.Infrastructure.Email.Ches;

public sealed class ChesAccessTokenProviderTests
{
    [Fact]
    public async Task GetAccessTokenAsync_FirstAndSubsequentRequest_AcquiresOnceAndReusesValidToken()
    {
        var handler = new SequenceHttpMessageHandler(_ => CreateTokenResponse("token-one", expiresIn: 300));
        var provider = CreateProvider(handler);

        var first = await provider.GetAccessTokenAsync(TestContext.Current.CancellationToken);
        var second = await provider.GetAccessTokenAsync(TestContext.Current.CancellationToken);

        Assert.Equal("token-one", first);
        Assert.Equal("token-one", second);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenExpirationEntersRefreshSkew_UsesExpiresInAndRefreshesToken()
    {
        var handler = new SequenceHttpMessageHandler(call => CreateTokenResponse($"token-{call}", expiresIn: 120));
        var timeProvider = new MutableTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var provider = CreateProvider(handler, timeProvider);

        var first = await provider.GetAccessTokenAsync(TestContext.Current.CancellationToken);
        timeProvider.Advance(TimeSpan.FromSeconds(59));
        var beforeSkew = await provider.GetAccessTokenAsync(TestContext.Current.CancellationToken);
        timeProvider.Advance(TimeSpan.FromSeconds(2));
        var insideSkew = await provider.GetAccessTokenAsync(TestContext.Current.CancellationToken);

        Assert.Equal("token-1", first);
        Assert.Equal("token-1", beforeSkew);
        Assert.Equal("token-2", insideSkew);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ConcurrentCallers_UseSingleTokenRequest()
    {
        var handler = new GatedTokenHttpMessageHandler();
        var provider = CreateProvider(handler);

        var requests = Enumerable
            .Range(0, 10)
            .Select(_ => provider.GetAccessTokenAsync(TestContext.Current.CancellationToken))
            .ToArray();
        await handler.RequestStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        handler.ReleaseResponse.TrySetResult();
        var tokens = await Task.WhenAll(requests);

        Assert.All(tokens, token => Assert.Equal("shared-token", token));
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task RequestTokenAsync_CallerCancellation_PropagatesToHttpRequest()
    {
        var handler = new CancellationAwareHttpMessageHandler();
        var client = CreateTokenClient(handler);
        using var cancellation = new CancellationTokenSource();

        var request = client.RequestTokenAsync(cancellation.Token);
        await handler.RequestStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        Assert.True(handler.ObservedCancellation);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task RequestTokenAsync_ResponseBodyStalls_ConfiguredPipelineTimesOutCompleteOperation()
    {
        var responseStream = new StallingReadStream();
        var content = new StreamContent(responseStream);
        content.Headers.ContentType = new("application/json");
        var handler = new SequenceHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = content,
        });
        var options = CreateOptions();
        var pipeline = new ResiliencePipelineBuilder<ChesAccessToken>()
            .AddTimeout(TimeSpan.FromMilliseconds(25))
            .Build();
        var httpClient = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var client = new ChesTokenClient(new StaticHttpClientFactory(httpClient), Options.Create(options), pipeline);

        var exception = await Assert.ThrowsAsync<ChesAuthenticationException>(() =>
            client.RequestTokenAsync(TestContext.Current.CancellationToken)
        );

        Assert.IsType<TimeoutRejectedException>(exception.InnerException);
        Assert.True(responseStream.ReadStarted.Task.IsCompleted);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task RequestTokenAsync_TransientFailure_RetriesAndReturnsToken()
    {
        var handler = new SequenceHttpMessageHandler(call =>
            call == 1
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : CreateTokenResponse("retried-token", expiresIn: 300)
        );
        var client = CreateTokenClient(handler);

        var token = await client.RequestTokenAsync(TestContext.Current.CancellationToken);

        Assert.Equal("retried-token", token.AccessToken);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task RequestTokenAsync_PermanentAuthenticationFailure_DoesNotRetry()
    {
        var handler = new SequenceHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var client = CreateTokenClient(handler);

        var exception = await Assert.ThrowsAsync<ChesAuthenticationException>(() =>
            client.RequestTokenAsync(TestContext.Current.CancellationToken)
        );

        Assert.Equal((int)HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task Invalidate_RejectedCachedToken_ForcesNextRequestToAcquireNewToken()
    {
        var handler = new SequenceHttpMessageHandler(call => CreateTokenResponse($"token-{call}", expiresIn: 300));
        var provider = CreateProvider(handler);
        var first = await provider.GetAccessTokenAsync(TestContext.Current.CancellationToken);

        provider.Invalidate(first);
        var second = await provider.GetAccessTokenAsync(TestContext.Current.CancellationToken);

        Assert.Equal("token-1", first);
        Assert.Equal("token-2", second);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task TokenLogging_DoesNotContainAccessTokenOrClientSecret()
    {
        const string accessToken = "sensitive-access-token";
        const string clientSecret = "sensitive-client-secret";
        var handler = new SequenceHttpMessageHandler(_ => CreateTokenResponse(accessToken, expiresIn: 300));
        var logger = new RecordingLogger<ChesAccessTokenProvider>();
        var options = CreateOptions();
        options.ClientSecret = clientSecret;
        var provider = CreateProvider(handler, options: options, logger: logger);

        await provider.GetAccessTokenAsync(TestContext.Current.CancellationToken);

        var logText = string.Join(Environment.NewLine, logger.Entries.Select(entry => entry.Message));
        Assert.DoesNotContain(accessToken, logText, StringComparison.Ordinal);
        Assert.DoesNotContain(clientSecret, logText, StringComparison.Ordinal);
    }

    private static ChesAccessTokenProvider CreateProvider(
        HttpMessageHandler handler,
        MutableTimeProvider? timeProvider = null,
        ChesOptions? options = null,
        RecordingLogger<ChesAccessTokenProvider>? logger = null
    )
    {
        options ??= CreateOptions();
        return new ChesAccessTokenProvider(
            CreateTokenClient(handler, options),
            Options.Create(options),
            timeProvider ?? new MutableTimeProvider(DateTimeOffset.UtcNow),
            logger ?? new RecordingLogger<ChesAccessTokenProvider>()
        );
    }

    private static ChesTokenClient CreateTokenClient(HttpMessageHandler handler, ChesOptions? options = null)
    {
        options ??= CreateOptions();
        var httpClient = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        return new ChesTokenClient(new StaticHttpClientFactory(httpClient), Options.Create(options));
    }

    private static ChesOptions CreateOptions() =>
        new()
        {
            AuthUrl = "https://auth.example.com/token",
            ClientId = "client-id",
            ClientSecret = "client-secret",
            TokenRefreshSkewSeconds = 60,
            TimeoutSeconds = 30,
        };

    private static HttpResponseMessage CreateTokenResponse(string accessToken, int expiresIn) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"access_token":"{{accessToken}}","expires_in":{{expiresIn}}}""",
                Encoding.UTF8,
                "application/json"
            ),
        };

    private sealed class StaticHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class SequenceHttpMessageHandler(Func<int, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            CallCount++;
            return Task.FromResult(responseFactory(CallCount));
        }
    }

    private sealed class GatedTokenHttpMessageHandler : HttpMessageHandler
    {
        public TaskCompletionSource RequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseResponse { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            CallCount++;
            RequestStarted.TrySetResult();
            await ReleaseResponse.Task.WaitAsync(cancellationToken);
            return CreateTokenResponse("shared-token", expiresIn: 300);
        }
    }

    private sealed class CancellationAwareHttpMessageHandler : HttpMessageHandler
    {
        public TaskCompletionSource RequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool ObservedCancellation { get; private set; }

        public int CallCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            CallCount++;
            RequestStarted.TrySetResult();

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("The cancellation-aware handler unexpectedly completed.");
            }
            catch (OperationCanceledException)
            {
                ObservedCancellation = true;
                throw;
            }
        }
    }

    private sealed class StallingReadStream : Stream
    {
        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        ) => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default
        )
        {
            ReadStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration)
        {
            _utcNow = _utcNow.Add(duration);
        }
    }
}
