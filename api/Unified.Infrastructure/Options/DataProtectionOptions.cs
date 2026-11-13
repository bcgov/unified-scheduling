using System.ComponentModel.DataAnnotations;

namespace Unified.Infrastructure.Options;

public class DataProtectionOptions
{
    public const string SectionName = "DataProtection";

    [Required(ErrorMessage = "DataProtection ApplicationName is required")]
    public string ApplicationName { get; set; } = "UnifiedScheduling";

    [Required(ErrorMessage = "DataProtection EncryptionKey is required")]
    public string EncryptionKey { get; set; } = string.Empty;
}
