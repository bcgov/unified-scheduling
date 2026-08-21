using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace Unified.Infrastructure.Email.Ches;

internal sealed class ChesTokenClient
{
    public const string HttpClientName = "ChesToken";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ChesOptions _options;
    private readonly ResiliencePipeline<ChesAccessToken> _resiliencePipeline;

    public ChesTokenClient(IHttpClientFactory httpClientFactory, IOptions<ChesOptions> options)
        : this(httpClientFactory, options, CreateResiliencePipeline(TimeSpan.FromSeconds(options.Value.TimeoutSeconds)))
    { }

    internal ChesTokenClient(
        IHttpClientFactory httpClientFactory,
        IOptions<ChesOptions> options,
        ResiliencePipeline<ChesAccessToken> resiliencePipeline
    )
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _resiliencePipeline = resiliencePipeline;
    }

    public async Task<ChesAccessToken> RequestTokenAsync(CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        try
        {
            return await _resiliencePipeline.ExecuteAsync(
                token => new ValueTask<ChesAccessToken>(RequestTokenOnceAsync(client, token)),
                cancellationToken
            );
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TimeoutRejectedException)
        {
            throw new ChesAuthenticationException("CHES access-token acquisition failed.", exception);
        }
    }

    private async Task<ChesAccessToken> RequestTokenOnceAsync(HttpClient client, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.AuthUrl)
        {
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string> { ["grant_type"] = "client_credentials" }
            ),
        };

        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}")
        );
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );

        if (!response.IsSuccessStatusCode)
            throw new ChesAuthenticationException((int)response.StatusCode);

        ChesTokenResponse? tokenResponse;
        try
        {
            tokenResponse = await response.Content.ReadFromJsonAsync<ChesTokenResponse>(cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new ChesAuthenticationException("CHES returned an invalid access-token response.", exception);
        }

        if (string.IsNullOrWhiteSpace(tokenResponse?.AccessToken))
            throw new ChesAuthenticationException("CHES returned an access-token response without a token.");

        return new ChesAccessToken(tokenResponse.AccessToken, tokenResponse.ExpiresIn);
    }

    private static ResiliencePipeline<ChesAccessToken> CreateResiliencePipeline(TimeSpan timeout) =>
        new ResiliencePipelineBuilder<ChesAccessToken>()
            .AddRetry(
                new RetryStrategyOptions<ChesAccessToken>
                {
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromMilliseconds(200),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    ShouldHandle = new PredicateBuilder<ChesAccessToken>()
                        .Handle<HttpRequestException>()
                        .Handle<IOException>()
                        .Handle<TimeoutRejectedException>()
                        .Handle<ChesAuthenticationException>(exception => exception.StatusCode is 408 or 429 or >= 500),
                }
            )
            .AddTimeout(timeout)
            .Build();

    private sealed record ChesTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }
}

internal sealed record ChesAccessToken(string AccessToken, int ExpiresInSeconds);
