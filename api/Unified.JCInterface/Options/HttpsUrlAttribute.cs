using System.ComponentModel.DataAnnotations;

namespace Unified.JCInterface.Options;

/// <summary>
/// Validates that a string is an absolute HTTPS URL. Used on <see cref="JCInterfaceOptions.Url"/>
/// since Basic Auth credentials are sent on every request — plain HTTP would expose them
/// in transit. The built-in <see cref="UrlAttribute"/> only checks general URL format and
/// does not enforce a specific scheme.
/// </summary>
public sealed class HttpsUrlAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        return value is string url
            && Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps;
    }
}
