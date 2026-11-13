using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Unified.Infrastructure.Encryption;
using DataProtectionOptions = Unified.Infrastructure.Options.DataProtectionOptions;

namespace Unified.Infrastructure;

public static class DataProtectionServiceCollectionExtensions
{
    public static IDataProtectionBuilder AddUnifiedDataProtection(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .AddOptions<DataProtectionOptions>()
            .BindConfiguration(DataProtectionOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(
                options => IsValidEncryptionKey(options.EncryptionKey),
                "DataProtection EncryptionKey must be valid Base64 encoding exactly 32 bytes (256 bits)."
            )
            .ValidateOnStart();

        var applicationName =
            configuration
                .GetSection(DataProtectionOptions.SectionName)
                .GetValue<string>(nameof(DataProtectionOptions.ApplicationName))
            ?? new DataProtectionOptions().ApplicationName;

        var builder = services.AddDataProtection().SetApplicationName(applicationName);
        services.AddSingleton<AesGcmXmlEncryptor>();
        services
            .AddOptions<KeyManagementOptions>()
            .Configure<AesGcmXmlEncryptor>((options, encryptor) => options.XmlEncryptor = encryptor);

        return builder;
    }

    private static bool IsValidEncryptionKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        Span<byte> decoded = stackalloc byte[32];
        try
        {
            return Convert.TryFromBase64String(value, decoded, out var written) && written == 32;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(decoded);
        }
    }
}
