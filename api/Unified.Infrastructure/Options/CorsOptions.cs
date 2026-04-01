using System.ComponentModel.DataAnnotations;

namespace Unified.Infrastructure.Options;

/// <summary>
/// Configuration options for Cross-Origin Resource Sharing (CORS).
/// </summary>
public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    /// <summary>
    /// Comma-separated list of allowed origins for CORS requests.
    /// Example: "http://localhost:8080,https://example.com"
    /// </summary>
    [Required(ErrorMessage = "CORS domain configuration is required")]
    public string AllowedOrigins { get; set; } = default!;
}
