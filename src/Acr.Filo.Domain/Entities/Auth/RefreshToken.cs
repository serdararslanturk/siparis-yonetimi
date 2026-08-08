namespace Acr.Filo.Domain.Entities.Auth;

/// <summary>SQL: dbo.RefreshTokens. Ham token DB'de TUTULMAZ; yalnız SHA-256 özeti.</summary>
public class RefreshToken
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public byte[] TokenHash { get; set; } = null!;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedByIp { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? RevokedReason { get; set; }
    /// <summary>Rotasyon zinciri: bu token hangi yeni token ile değiştirildi.</summary>
    public long? ReplacedById { get; set; }

    public User User { get; set; } = null!;

    public bool IsActive(DateTime utcNow) => RevokedAtUtc is null && ExpiresAtUtc > utcNow;
}
