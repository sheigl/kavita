using System;
using System.Security.Cryptography;

namespace Kavita.Common.Helpers;

/// <summary>
/// AES-GCM based encryption/decryption utility for sensitive data at rest.
/// Stores nonce + tag + ciphertext in a single base64 string.
/// </summary>
public static class CryptoHelper
{
    private const int KeySize = 32; // 256-bit key
    private const int NonceSize = 12; // standard GCM nonce size (96 bits)
    private const int TagSize = 16;   // standard GCM tag size (128 bits)

    /// <summary>
    /// Generates a random encryption key and returns it as a base64 string.
    /// </summary>
    public static string GenerateKey()
    {
        byte[] key = new byte[KeySize];
        RandomNumberGenerator.Fill(key);
        return Convert.ToBase64String(key);
    }

    /// <summary>
    /// Encrypts plaintext using the provided base64-encoded key.
    /// Returns "nonce:tag:ciphertext" as a base64 string.
    /// </summary>
    public static string Encrypt(string plainText, string base64Key)
    {
        byte[] key = Convert.FromBase64String(base64Key);
        byte[] nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);
        byte[] plaintextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);

        byte[] ciphertext = new byte[plaintextBytes.Length];
        byte[] tag = new byte[TagSize];
        using (AesGcm aes = new(key, TagSize))
        {
            aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);
        }

        // Combine nonce + tag + ciphertext into a single base64 string
        byte[] combined = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, combined, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, combined, nonce.Length + tag.Length, ciphertext.Length);

        return Convert.ToBase64String(combined);
    }

    /// <summary>
    /// Decrypts a base64-encoded encrypted string using the provided base64-encoded key.
    /// </summary>
    public static string Decrypt(string encryptedBase64, string base64Key)
    {
        byte[] combined = Convert.FromBase64String(encryptedBase64);
        byte[] key = Convert.FromBase64String(base64Key);

        int nonceLen = NonceSize; // 12 bytes
        int tagLen = TagSize;     // 16 bytes

        byte[] nonce = new byte[nonceLen];
        byte[] tag = new byte[tagLen];
        byte[] ciphertext = new byte[combined.Length - nonceLen - tagLen];

        Buffer.BlockCopy(combined, 0, nonce, 0, nonceLen);
        Buffer.BlockCopy(combined, nonceLen, tag, 0, tagLen);
        Buffer.BlockCopy(combined, nonceLen + tagLen, ciphertext, 0, ciphertext.Length);

        byte[] plaintextBytes = new byte[ciphertext.Length];
        using (AesGcm aes = new(key, TagSize))
        {
            aes.Decrypt(nonce, ciphertext, tag, plaintextBytes);
        }

        return System.Text.Encoding.UTF8.GetString(plaintextBytes);
    }
}
