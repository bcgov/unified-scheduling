using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Unified.JCInterface.Options;

namespace Unified.JCInterface.Services;

internal sealed class JCInterfaceLoggingHandler(
    ILogger<JCInterfaceLoggingHandler> logger,
    IOptions<JCInterfaceOptions> options
) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var requestUri = request.RequestUri;

        logger.LogInformation(
            "Sending JC Interface request {Method} {RequestUri} with timeout {Timeout}",
            request.Method,
            requestUri,
            options.Value.HttpTimeout
        );

        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            logger.LogInformation(
                "JC Interface request {Method} {RequestUri} completed with status {StatusCode} in {ElapsedMilliseconds} ms",
                request.Method,
                requestUri,
                (int)response.StatusCode,
                stopwatch.ElapsedMilliseconds
            );
            return response;
        }
        catch (OperationCanceledException exception)
        {
            logger.LogError(
                exception,
                "JC Interface request {Method} {RequestUri} was canceled after {ElapsedMilliseconds} ms; configured timeout is {Timeout}; caller cancellation requested: {CallerCancellationRequested}",
                request.Method,
                requestUri,
                stopwatch.ElapsedMilliseconds,
                options.Value.HttpTimeout,
                cancellationToken.IsCancellationRequested
            );
            throw;
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(
                exception,
                "JC Interface request {Method} {RequestUri} failed after {ElapsedMilliseconds} ms",
                request.Method,
                requestUri,
                stopwatch.ElapsedMilliseconds
            );
            throw;
        }
    }
}