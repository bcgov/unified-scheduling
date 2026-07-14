using System.Net;
using System.Text;

namespace Unified.Tests.TestHelpers;

/// <summary>
/// Minimal fake HTTP handler for typed HttpClient-based generated clients (e.g. LocationServicesClient).
/// Returns the same canned JSON body for every request, matched by request URL substring when needed.
/// </summary>
internal sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, string> responseFactory) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var json = responseFactory(request);
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        return Task.FromResult(response);
    }
}
