using System;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace Cinema.Foundation.Security;

/// <summary>
/// Production-grade password hasher using PBKDF2 with SHA-256 (128-bit salt, 100K iterations).
/// Replaces the previous unsalted SHA-256 implementation.
/// Format: {iterations}.{base64Salt}.{base64Hash}
/// </summary>
public static class PasswordHasher
{
    private const int SaltSize = 16; // 128-bit
    private const int HashSize = 32; // 256-bit
    private const int Iterations = 100_000;
    private const KeyDerivationPrf Prf = KeyDerivationPrf.HMACSHA256;
    private const char Separator = '.';

    public static string Hash(string input)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = KeyDerivation.Pbkdf2(input, salt, Prf, Iterations, HashSize);
        return $"{Iterations}{Separator}{Convert.ToHexString(salt)}{Separator}{Convert.ToHexString(hash)}";
    }

    public static bool Verify(string input, string? storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash)) return false;

        // Support legacy unsalted SHA-256 hashes (64 hex chars, no dots)
        if (!storedHash.Contains(Separator))
        {
            return VerifyLegacySha256(input, storedHash);
        }

        try
        {
            var parts = storedHash.Split(Separator);
            if (parts.Length != 3) return false;

            if (!int.TryParse(parts[0], out var iterations)) return false;
            var salt = DecodeBytes(parts[1]);
            var expectedHash = DecodeBytes(parts[2]);

            var actualHash = KeyDerivation.Pbkdf2(input, salt, Prf, iterations, expectedHash.Length);
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch
        {
            return false;
        }
    }

    private static byte[] DecodeBytes(string encoded)
    {
        try
        {
            if (encoded.Length % 2 == 0)
            {
                return Convert.FromHexString(encoded);
            }
        }
        catch
        {
            // fallback to base64 if not valid hex
        }

        return Convert.FromBase64String(encoded);
    }

    /// <summary>
    /// Backward-compatible verification for legacy unsalted SHA-256 hashes 
    /// stored in the database seed data. These should be migrated on next login.
    /// </summary>
    private static bool VerifyLegacySha256(string input, string storedHash)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        var computed = Convert.ToHexString(bytes).ToLowerInvariant();
        return string.Equals(computed, storedHash, StringComparison.OrdinalIgnoreCase);
    }
}
