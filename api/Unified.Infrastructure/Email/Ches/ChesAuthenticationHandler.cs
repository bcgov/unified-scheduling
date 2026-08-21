using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace Unified.Infrastructure.Email.Ches;

internal sealed class ChesAuthenticationHandler : DelegatingHandler
{
    private readonly IChesAccessTokenProvider _tokenProvider;
    private readonly TimeSpan _submissionTimeout;

    public ChesAuthenticationHandler(IChesAccessTokenProvider tokenProvider, IOptions<ChesOptions> options)
        : this(tokenProvider, TimeSpan.FromSeconds(options.Value.TimeoutSeconds)) { }

    internal ChesAuthenticationHandler(IChesAccessTokenProvider tokenProvider, TimeSpan submissionTimeout)
    {
        _tokenProvider = tokenProvider;
        _submissionTimeout = submissionTimeout;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var accessToken = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        if (!IsEmailSubmission(request))
        {
            var response = await base.SendAsync(request, cancellationToken);
            InvalidateRejectedToken(response, accessToken);
            return response;
        }

        var submissionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        submissionCancellation.CancelAfter(_submissionTimeout);

        try
        {
            var response = await base.SendAsync(request, submissionCancellation.Token);

            InvalidateRejectedToken(response, accessToken);

            response.Content = new ChesResponseContent(
                response.Content,
                response.IsSuccessStatusCode,
                (int)response.StatusCode,
                submissionCancellation
            );

            return response;
        }
        catch (Exception exception)
            when (IsEmailSubmission(request)
                && exception
                    is HttpRequestException
                        or OperationCanceledException
                        or Polly.Timeout.TimeoutRejectedException
            )
        {
            submissionCancellation.Dispose();
            throw new ChesPostOutcomeUnknownException(exception);
        }
        catch
        {
            submissionCancellation.Dispose();
            throw;
        }
    }

    private static bool IsEmailSubmission(HttpRequestMessage request) =>
        request.Method == HttpMethod.Post
        && request.RequestUri?.AbsolutePath.TrimEnd('/').EndsWith("/email", StringComparison.OrdinalIgnoreCase) == true;

    private void InvalidateRejectedToken(HttpResponseMessage response, string accessToken)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            _tokenProvider.Invalidate(accessToken);
    }
}

internal sealed class ChesPostOutcomeUnknownException(Exception innerException)
    : Exception("The CHES email submission transport failed after dispatch began.", innerException);

internal sealed class ChesResponseReadException(int statusCode, Exception innerException)
    : Exception("The CHES error response could not be read within the configured timeout.", innerException)
{
    public int StatusCode { get; } = statusCode;
}
