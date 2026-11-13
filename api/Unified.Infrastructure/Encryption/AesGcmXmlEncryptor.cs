using System.Security.Cryptography;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.Options;
using Unified.Infrastructure.Options;

namespace Unified.Infrastructure.Encryption;

public sealed class AesGcmXmlEncryptor(IOptions<DataProtectionOptions> options) : IXmlEncryptor
{
    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        ArgumentNullException.ThrowIfNull(plaintextElement);

        var key = Convert.FromBase64String(options.Value.EncryptionKey);
        try
        {
            using var aes = new AesGcmService(key);
            return new EncryptedXmlInfo(
                new XElement("encryptedKey", new XElement("value", aes.Encrypt(plaintextElement.ToString()))),
                typeof(AesGcmXmlDecryptor)
            );
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }
}
