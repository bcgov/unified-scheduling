using System;
using Microsoft.Extensions.Logging;

namespace Unified.Infrastructure.Helpers
{
    public static class XForwardedForHelper
    {
        private static readonly ILogger _logger;

        static XForwardedForHelper()
        {
            using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole());
            _logger = factory.CreateLogger("XForwardedForHelper");
        }

        public static string BuildUrlString(
            string forwardedProto,
            string forwardedHost,
            string forwardedPort,
            string baseUrl,
            string remainingPath = "",
            string query = ""
        )
        {
            // Default: Assume the code is running locally, unless specified.
            forwardedProto = string.IsNullOrEmpty(forwardedProto) ? "http" : forwardedProto;
            forwardedHost = string.IsNullOrEmpty(forwardedHost) ? "localhost" : forwardedHost;
            forwardedPort = string.IsNullOrEmpty(forwardedPort) ? "8080" : forwardedPort;
            baseUrl = string.IsNullOrEmpty(baseUrl) ? "/" : baseUrl;

            var sanitizedPath = baseUrl;
            var isLocalhost = forwardedHost.Contains("localhost");
            if (!string.IsNullOrEmpty(remainingPath))
            {
                sanitizedPath = string.Format("{0}/{1}", baseUrl.TrimEnd('/'), remainingPath.TrimStart('/'));
            }

            var uriBuilder = new UriBuilder
            {
                Scheme = forwardedProto,
                Host = forwardedHost,
                Path = sanitizedPath,
                Query = query,
            };

            // Prevent removing the 8080 on localhost
            var portComponent =
                string.IsNullOrEmpty(forwardedPort)
                || forwardedPort == "80"
                || forwardedPort == "443"
                || (forwardedPort == "8080" && !isLocalhost)
                    ? ""
                    : $":{forwardedPort}";

            if (!string.IsNullOrEmpty(portComponent))
            {
                int port;
                int.TryParse(forwardedPort, out port);
                uriBuilder.Port = port;
            }

            _logger.LogInformation($"uriBuilder.Uri.AbsoluteUri `{uriBuilder.Uri.AbsoluteUri}`");
            return uriBuilder.Uri.AbsoluteUri;
        }
    }
}
