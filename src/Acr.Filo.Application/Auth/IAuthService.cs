using Acr.Filo.Application.Common;

namespace Acr.Filo.Application.Auth;

public interface IAuthService
{
    Task<Result<LoginResponse>> LoginAsync(LoginRequest req, string? ip, string? userAgent, string? correlationId, CancellationToken ct);
    Task<Result<LoginResponse>> RefreshAsync(RefreshRequest req, string? ip, CancellationToken ct);
    Task<Result> LogoutAsync(string refreshToken, string? ip, CancellationToken ct);
    Task<Result> ChangePasswordAsync(int userId, ChangePasswordRequest req, CancellationToken ct);
    Task<Result<UserInfo>> GetMeAsync(int userId, CancellationToken ct);
}

/// <summary>Parola hash'leme/doğrulama. Infrastructure PBKDF2 ile uygular.</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    /// <summary>rehashNeeded=true ise başarılı doğrulama sonrası hash güncellenmeli.</summary>
    bool Verify(string password, string hash, out bool rehashNeeded);
}

/// <summary>JWT üretimi. Infrastructure imzalar.</summary>
public interface ITokenService
{
    (string token, DateTime expiresUtc) CreateAccessToken(int userId, string email, IEnumerable<string> roles, IEnumerable<string> permissions);
    /// <summary>Ham refresh token (istemciye) + SHA-256 özeti (DB'ye).</summary>
    (string raw, byte[] hash, DateTime expiresUtc) CreateRefreshToken();
    byte[] HashRefreshToken(string raw);
}
