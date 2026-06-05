using System;
using Microsoft.AspNetCore.Http;

namespace Unified.Infrastructure.Helpers
{
    public static class XForwardedForHelper
    {
        private const string DefaultLocalhost = "localhost";

        public static string BuildUrlString(
            string forwardedProto,
            string forwardedHost,
            string forwardedPort,
            string baseUrl,
            string remainingPath = "",
            string query = ""
        )
        {
            var hasExplicitForwardedPort = !string.IsNullOrWhiteSpace(forwardedPort);

            forwardedProto = NormalizeForwardedProto(forwardedProto);
            forwardedHost = NormalizeForwardedValue(forwardedHost, DefaultLocalhost);

            var normalizedPort = hasExplicitForwardedPort
                ? NormalizeForwardedPort(forwardedPort, "")
                : "";

            forwardedHost = NormalizeHostAndPort(
                forwardedProto,
                forwardedHost,
                ref normalizedPort
            );

            forwardedPort = string.IsNullOrWhiteSpace(normalizedPort) ? "8080" : normalizedPort;

            var sanitizedPath = baseUrl;
            var isLocalhost = IsLocalHost(forwardedHost);
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

            if (!string.IsNullOrEmpty(portComponent) && int.TryParse(forwardedPort, out var port) && port > 0)
            {
                uriBuilder.Port = port;
            }

            try
            {
                return uriBuilder.Uri.AbsoluteUri;
            }
            catch (UriFormatException)
            {
                var fallbackUriBuilder = new UriBuilder
                {
                    Scheme = forwardedProto,
                    Host = DefaultLocalhost,
                    Path = sanitizedPath,
                    Query = query,
                };

                if (int.TryParse(forwardedPort, out var fallbackPort) && fallbackPort > 0)
                {
                    fallbackUriBuilder.Port = fallbackPort;
                }

                return fallbackUriBuilder.Uri.AbsoluteUri;
            }
        }

        private static bool IsLocalHost(string? host)
        {
            if (string.IsNullOrWhiteSpace(host))
                return false;

            var normalizedHost = host.Trim();

            return string.Equals(normalizedHost, "localhost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedHost, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedHost, "::1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedHost, "[::1]", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeForwardedValue(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            var normalized = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(normalized))
                return fallback;

            return normalized.Trim().Trim('\'', '"');
        }

        private static string NormalizeForwardedProto(string value)
        {
            var normalized = NormalizeForwardedValue(value, "http");
            return string.Equals(normalized, "https", StringComparison.OrdinalIgnoreCase) ? "https" : "http";
        }

        private static string NormalizeForwardedPort(string value, string fallback)
        {
            var normalized = NormalizeForwardedValue(value, fallback);
            return int.TryParse(normalized, out var port) && port > 0 ? port.ToString() : fallback;
        }

        private static string NormalizeHostAndPort(string forwardedProto, string forwardedHost, ref string forwardedPort)
        {
            forwardedHost = forwardedHost.Trim().Trim('\'', '"');

            if (Uri.TryCreate(forwardedHost, UriKind.Absolute, out var absoluteHostUri))
            {
                if (string.IsNullOrWhiteSpace(forwardedPort) && !absoluteHostUri.IsDefaultPort)
                    forwardedPort = absoluteHostUri.Port.ToString();

                return NormalizeValidatedHost(absoluteHostUri.Host);
            }

            if (Uri.TryCreate($"{forwardedProto}://{forwardedHost}", UriKind.Absolute, out var hostWithPortUri))
            {
                if (string.IsNullOrWhiteSpace(forwardedPort) && !hostWithPortUri.IsDefaultPort)
                    forwardedPort = hostWithPortUri.Port.ToString();

                return NormalizeValidatedHost(hostWithPortUri.Host);
            }

            if (TrySplitHostAndPort(forwardedHost, out var hostOnly, out var parsedPort))
            {
                if (string.IsNullOrWhiteSpace(forwardedPort) && parsedPort > 0)
                    forwardedPort = parsedPort.ToString();

                return NormalizeValidatedHost(hostOnly);
            }

            return NormalizeValidatedHost(forwardedHost);
        }

        private static bool TrySplitHostAndPort(string value, out string host, out int port)
        {
            host = value;
            port = 0;

            var lastColonIndex = value.LastIndexOf(':');
            if (lastColonIndex <= 0 || value.Contains(']'))
                return false;

            var hostPart = value[..lastColonIndex];
            var portPart = value[(lastColonIndex + 1)..];
            if (!int.TryParse(portPart, out port) || port <= 0)
                return false;

            host = hostPart;
            return true;
        }

        private static string NormalizeValidatedHost(string value)
        {
            var normalized = value.Trim().Trim('\'', '"').TrimEnd('/');
            return Uri.CheckHostName(normalized) == UriHostNameType.Unknown ? DefaultLocalhost : normalized;
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

            if (!trimmed.StartsWith('/'))
                trimmed = "/" + trimmed;

            if (!trimmed.EndsWith('/'))
                trimmed += "/";

            return trimmed;
        }
    }
}
