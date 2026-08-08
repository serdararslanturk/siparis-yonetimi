using System.Security.Cryptography;
using Acr.Filo.Application.Auth;

namespace Acr.Filo.Infrastructure.Identity;

/// <summary>
/// PBKDF2-HMAC-SHA256, 100.000 tur. Sıfır dış bağımlılık (yalnızca BCL).
/// Format: {algo}.{iter}.{saltB64}.{hashB64}  ->  "PBKDF2.100000.<salt>.<hash>"
/// Şartname madde 6: şifreler güçlü algoritmayla hashlenir; açık metin saklanmaz.
///
/// NEDEN tam ASP.NET Core Identity değil: Identity 7 tablo (AspNetUsers...) getirir,
/// şemamızla çakışır. Sadece hash algoritmasını burada birebir uyguluyoruz.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;    // 128-bit
    private const int HashSize = 32;    // 256-bit
    private const string Prefix = "PBKDF2";

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Prefix}.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string hash, out bool rehashNeeded)
    {
        rehashNeeded = false;
        if (string.IsNullOrEmpty(hash)) return false;

        var parts = hash.Split('.');
        if (parts.Length != 4 || parts[0] != Prefix) return false;
        if (!int.TryParse(parts[1], out var iter)) return false;

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException) { return false; }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iter, HashAlgorithmName.SHA256, expected.Length);
        // Sabit zamanlı karşılaştırma — timing attack'e karşı.
        var ok = CryptographicOperations.FixedTimeEquals(actual, expected);
        if (ok && iter != Iterations) rehashNeeded = true; // tur sayısı artmışsa güncelle
        return ok;
    }
}
