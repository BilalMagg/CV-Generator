using System.Security.Cryptography;
using CV_Generator.Services;

namespace CV_Generator.Services;

public class AesEncryptionService : IAesEncryptionService
{
    private byte[]? _key;

    /// <summary>The key is resolved lazily on first use so services that merely
    /// depend on this class (e.g. LLM key storage) do not fail construction in
    /// environments that simply haven't configured Gmail.</summary>
    private byte[] GetKey()
    {
        if (_key is not null) return _key;
        var keyString = Environment.GetEnvironmentVariable("GMAIL_TOKEN_ENCRYPTION_KEY")
            ?? throw new InvalidOperationException("GMAIL_TOKEN_ENCRYPTION_KEY is not set");
        _key = Convert.FromBase64String(keyString);
        return _key;
    }

    public string Encrypt(string plainText)
    {
        var iv = RandomNumberGenerator.GetBytes(16);
        using var aes = Aes.Create();
        aes.Key = GetKey();
        aes.IV = iv;

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var result = new byte[iv.Length + cipherBytes.Length];
        Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, iv.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string cipherText)
    {
        var combined = Convert.FromBase64String(cipherText);
        var iv = combined[..16];
        var cipherBytes = combined[16..];

        using var aes = Aes.Create();
        aes.Key = GetKey();
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return System.Text.Encoding.UTF8.GetString(plainBytes);
    }
}
