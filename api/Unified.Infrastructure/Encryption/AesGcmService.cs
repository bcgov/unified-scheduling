using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Unified.Infrastructure.Encryption;

// Uses JASPER's Base64 envelope: nonce length, nonce, tag length, tag, ciphertext.
public sealed class AesGcmService(byte[] key) : IDisposable
{
    private readonly AesGcm aes = new(key, AesGcm.TagByteSizes.MaxSize);

    public string Encrypt(string plaintext)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonceSize = AesGcm.NonceByteSizes.MaxSize;
        var tagSize = AesGcm.TagByteSizes.MaxSize;
        var encrypted = new byte[4 + nonceSize + 4 + tagSize + plainBytes.Length];
        BinaryPrimitives.WriteInt32LittleEndian(encrypted.AsSpan(0, 4), nonceSize);
        BinaryPrimitives.WriteInt32LittleEndian(encrypted.AsSpan(4 + nonceSize, 4), tagSize);
        var nonce = encrypted.AsSpan(4, nonceSize);
        var tag = encrypted.AsSpan(4 + nonceSize + 4, tagSize);
        var ciphertext = encrypted.AsSpan(4 + nonceSize + 4 + tagSize);
        RandomNumberGenerator.Fill(nonce);

        try
        {
            aes.Encrypt(nonce, plainBytes, ciphertext, tag);
            return Convert.ToBase64String(encrypted);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBytes);
        }
    }

    public string Decrypt(string ciphertext)
    {
        var encrypted = Convert.FromBase64String(ciphertext);
        var nonceSize = AesGcm.NonceByteSizes.MaxSize;
        var tagSize = AesGcm.TagByteSizes.MaxSize;
        var headerSize = 4 + nonceSize + 4 + tagSize;
        if (
            encrypted.Length < headerSize
            || BinaryPrimitives.ReadInt32LittleEndian(encrypted.AsSpan(0, 4)) != nonceSize
            || BinaryPrimitives.ReadInt32LittleEndian(encrypted.AsSpan(4 + nonceSize, 4)) != tagSize
        )
            throw new CryptographicException("Invalid AES-GCM encrypted key envelope.");

        var plaintext = new byte[encrypted.Length - headerSize];
        try
        {
            aes.Decrypt(
                encrypted.AsSpan(4, nonceSize),
                encrypted.AsSpan(headerSize),
                encrypted.AsSpan(4 + nonceSize + 4, tagSize),
                plaintext
            );
            return Encoding.UTF8.GetString(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public void Dispose() => aes.Dispose();
}
