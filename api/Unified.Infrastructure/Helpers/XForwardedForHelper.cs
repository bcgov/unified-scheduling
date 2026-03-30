using System;
using Microsoft.AspNetCore.Http;
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

            // _logger.LogInformation($"uriBuilder.Uri.AbsoluteUri `{uriBuilder.Uri.AbsoluteUri}`");
            return uriBuilder.Uri.AbsoluteUri;
        }

        public static string ResolveBaseHref(HttpRequest request)
        {
            if (request == null)
                return "/";

            if (request.Headers.TryGetValue("X-Base-Href", out var baseHrefValues))
            {
                var normalized = NormalizeBaseHref(baseHrefValues.ToString());
                if (normalized != "/")
                    return normalized;
            }

            if (request.PathBase.HasValue && !string.IsNullOrWhiteSpace(request.PathBase.Value))
            {
                return NormalizeBaseHref(request.PathBase.Value);
            }

            var pathValue = request.Path.Value;
            if (!string.IsNullOrEmpty(pathValue))
            {
                var segments = pathValue.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length > 1 && string.Equals(segments[1], "api", StringComparison.OrdinalIgnoreCase))
                {
                    return NormalizeBaseHref($"/{segments[0]}/");
                }
            }

            return "/";
        }

        public static string NormalizeBaseHref(string baseHref)
        {
            var trimmed = baseHref?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                return "/";

            if (!trimmed.StartsWith("/"))
                trimmed = "/" + trimmed;

            if (!trimmed.EndsWith("/"))
                trimmed += "/";

            return trimmed;
        }
    }
}
