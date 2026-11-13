using System.Security.Cryptography;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Unified.Infrastructure.Options;

namespace Unified.Infrastructure.Encryption;

// Data Protection reactivates this public type from persisted XML after a restart.
public sealed class AesGcmXmlDecryptor(IServiceProvider services) : IXmlDecryptor
{
    public XElement Decrypt(XElement encryptedElement)
    {
        ArgumentNullException.ThrowIfNull(encryptedElement);

        var value =
            encryptedElement.Element("value")?.Value
            ?? throw new CryptographicException("Encrypted Data Protection key has no value.");
        var options = services.GetRequiredService<IOptions<DataProtectionOptions>>().Value;
        var key = Convert.FromBase64String(options.EncryptionKey);
        try
        {
            using var aes = new AesGcmService(key);
            return XElement.Parse(aes.Decrypt(value));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }
}
