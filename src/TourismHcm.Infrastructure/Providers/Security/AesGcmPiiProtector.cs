using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TourismHcm.Application.Abstractions.Providers;
using TourismHcm.Application.Options;

namespace TourismHcm.Infrastructure.Providers.Security;

/// <summary>Encrypts PII with AES-GCM; stored as <c>v1:</c> + base64(nonce|tag|ciphertext).</summary>
public sealed class AesGcmPiiProtector : IPiiProtector
{
    private readonly byte[] _key;

    public AesGcmPiiProtector(IOptions<PiiOptions> options)
    {
        var configured = options.Value.EncryptionKey;
        if (string.IsNullOrWhiteSpace(configured))
        {
            // Deterministic dev fallback so local runs work without secrets.
            _key = SHA256.HashData(Encoding.UTF8.GetBytes("TourismHcm.Dev.Pii.Key"));
        }
        else
        {
            try
            {
                _key = Convert.FromBase64String(configured);
                if (_key.Length != 32)
                {
                    _key = SHA256.HashData(_key);
                }
            }
            catch (FormatException)
            {
                _key = SHA256.HashData(Encoding.UTF8.GetBytes(configured));
            }
        }
    }

    public string Encrypt(string plaintext)
    {
        var nonce = new byte[12];
        RandomNumberGenerator.Fill(nonce);
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(_key, 16);
        aes.Encrypt(nonce, plainBytes, cipher, tag);
        var packed = new byte[nonce.Length + tag.Length + cipher.Length];
        Buffer.BlockCopy(nonce, 0, packed, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, packed, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipher, 0, packed, nonce.Length + tag.Length, cipher.Length);
        return "v1:" + Convert.ToBase64String(packed);
    }

    public string? Decrypt(string? ciphertext)
    {
        if (string.IsNullOrWhiteSpace(ciphertext) || !ciphertext.StartsWith("v1:", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            var packed = Convert.FromBase64String(ciphertext[3..]);
            if (packed.Length < 12 + 16)
            {
                return null;
            }

            var nonce = packed.AsSpan(0, 12);
            var tag = packed.AsSpan(12, 16);
            var cipher = packed.AsSpan(28);
            var plain = new byte[cipher.Length];
            using var aes = new AesGcm(_key, 16);
            aes.Decrypt(nonce, cipher, tag, plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch
        {
            return null;
        }
    }
}
