using Acr.Filo.Domain.Common;

namespace Acr.Filo.Domain.Entities.Auth;

/// <summary>SQL: dbo.Users</summary>
public class User : ConcurrentAuditableEntity, ISoftDeletable
{
    public int Id { get; set; }

    /// <summary>SQL kolonu Latin1_General_100_CI_AS collation ile tanımlıdır.
    /// Turkish_CI_AS altında 'I' ile 'i' eşleşmez ve login kırılır.</summary>
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;

    /// <summary>NULL = parola atanmamış. Bu haldeki hesapla giriş yapılamaz
    /// (AuthService NULL hash'i erken reddeder). Bkz. db/03-seed.sql.</summary>
    public string? PasswordHash { get; set; }

    /// <summary>Parola/rol değişiminde yenilenir; mevcut refresh token'ları geçersizler.</summary>
    public Guid SecurityStamp { get; set; }

    public bool MustChangePassword { get; set; }
    public int AccessFailedCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
