namespace Unified.Infrastructure.Email.Ches;

internal interface IChesAccessTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);

    void Invalidate(string rejectedAccessToken);
}
