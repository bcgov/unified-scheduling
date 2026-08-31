using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Unified.Infrastructure.Email.Ches;

internal sealed class ChesAccessTokenProvider(
    ChesTokenClient tokenClient,
    IOptions<ChesOptions> options,
    TimeProvider timeProvider,
    ILogger<ChesAccessTokenProvider> logger
) : IChesAccessTokenProvider
{
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly TimeSpan _refreshSkew = TimeSpan.FromSeconds(options.Value.TokenRefreshSkewSeconds);
    private CachedToken? _cachedToken;

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var cachedToken = _cachedToken;
        if (IsUsable(cachedToken))
            return cachedToken!.AccessToken;

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            cachedToken = _cachedToken;
            if (IsUsable(cachedToken))
                return cachedToken!.AccessToken;

            var token = await tokenClient.RequestTokenAsync(cancellationToken);
            if (token.ExpiresInSeconds <= 0)
                throw new ChesAuthenticationException("CHES returned an invalid access-token lifetime.");

            _cachedToken = new CachedToken(
                token.AccessToken,
                timeProvider.GetUtcNow().AddSeconds(token.ExpiresInSeconds)
            );

            logger.LogDebug(
                "Refreshed CHES access token with lifetime {TokenLifetimeSeconds} seconds",
                token.ExpiresInSeconds
            );

            return token.AccessToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void Invalidate(string rejectedAccessToken)
    {
        while (true)
        {
            var cachedToken = Volatile.Read(ref _cachedToken);
            if (
                cachedToken is null
                || !string.Equals(cachedToken.AccessToken, rejectedAccessToken, StringComparison.Ordinal)
            )
            {
                return;
            }

            if (ReferenceEquals(Interlocked.CompareExchange(ref _cachedToken, null, cachedToken), cachedToken))
            {
                logger.LogDebug("Invalidated cached CHES access token after provider rejection");
                return;
            }
        }
    }

    private bool IsUsable(CachedToken? token) =>
        token is not null && timeProvider.GetUtcNow().Add(_refreshSkew) < token.ExpiresAt;

    private sealed record CachedToken(string AccessToken, DateTimeOffset ExpiresAt);
}
