using System.Security.Cryptography;

namespace StockDesk.Api.Auth;

/// <summary>
/// Hashing de contraseñas con PBKDF2 (Rfc2898DeriveBytes), sin dependencias externas.
/// </summary>
public static class PasswordHasher
{
    private const int SaltSize = 16; // 128 bits
    private const int KeySize = 32; // 256 bits
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public static (string hash, string salt) HashPassword(string password)
    {
        byte[] saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hashBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, Iterations, Algorithm, KeySize);
        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }

    public static bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        byte[] saltBytes = Convert.FromBase64String(storedSalt);
        byte[] expectedHash = Convert.FromBase64String(storedHash);
        byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, Iterations, Algorithm, KeySize);
        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }
}
