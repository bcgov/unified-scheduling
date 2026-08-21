using System.Net;
using Polly.Timeout;

namespace Unified.Infrastructure.Email.Ches;

/// <summary>
/// Keeps the POST submission deadline active after response headers are received. The generated
/// NSwag client uses ResponseHeadersRead, so its HttpClient pipeline no longer owns response-body
/// timeout enforcement after SendAsync returns.
/// </summary>
internal sealed class ChesResponseContent : HttpContent
{
    private readonly HttpContent _innerContent;
    private readonly bool _submissionWasAccepted;
    private readonly int _statusCode;
    private readonly CancellationTokenSource _submissionCancellation;

    public ChesResponseContent(
        HttpContent innerContent,
        bool submissionWasAccepted,
        int statusCode,
        CancellationTokenSource submissionCancellation
    )
    {
        _innerContent = innerContent;
        _submissionWasAccepted = submissionWasAccepted;
        _statusCode = statusCode;
        _submissionCancellation = submissionCancellation;

        foreach (var header in innerContent.Headers)
            Headers.TryAddWithoutValidation(header.Key, header.Value);
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        CopyToStreamAsync(stream, context, CancellationToken.None);

    protected override Task SerializeToStreamAsync(
        Stream stream,
        TransportContext? context,
        CancellationToken cancellationToken
    ) => CopyToStreamAsync(stream, context, cancellationToken);

    protected override bool TryComputeLength(out long length)
    {
        if (_innerContent.Headers.ContentLength is { } contentLength)
        {
            length = contentLength;
            return true;
        }

        length = 0;
        return false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _innerContent.Dispose();
            _submissionCancellation.Dispose();
        }

        base.Dispose(disposing);
    }

    private async Task CopyToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _submissionCancellation.Token
        );

        try
        {
            await _innerContent.CopyToAsync(stream, context, linkedCancellation.Token);
        }
        catch (Exception exception)
            when (exception is HttpRequestException or OperationCanceledException or TimeoutRejectedException)
        {
            if (_submissionWasAccepted)
                throw new ChesPostOutcomeUnknownException(exception);

            throw new ChesResponseReadException(_statusCode, exception);
        }
    }
}
