namespace Acr.Filo.Application.Auth;

/// <summary>
/// Parola politikası. appsettings Security:MinPasswordLength ile eşleşir (varsayılan 12).
/// Şartname madde 12: güçlü parola politikası.
/// </summary>
public static class PasswordPolicy
{
    public static string? Validate(string? password, int minLength)
    {
        if (string.IsNullOrWhiteSpace(password))
            return "Parola boş olamaz.";
        if (password.Length < minLength)
            return $"Parola en az {minLength} karakter olmalı.";
        if (!password.Any(char.IsUpper))
            return "Parola en az bir büyük harf içermeli.";
        if (!password.Any(char.IsLower))
            return "Parola en az bir küçük harf içermeli.";
        if (!password.Any(char.IsDigit))
            return "Parola en az bir rakam içermeli.";
        return null; // geçerli
    }
}
