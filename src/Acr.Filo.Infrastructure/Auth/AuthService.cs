using Acr.Filo.Application.Abstractions;
using Acr.Filo.Application.Auth;
using Acr.Filo.Application.Common;
using Acr.Filo.Domain.Entities.Auth;
using Acr.Filo.Infrastructure.Identity;
using Acr.Filo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Acr.Filo.Infrastructure.Auth;

public sealed class SecurityOptions
{
    public int MaxFailedAccessAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public int MinPasswordLength { get; set; } = 12;
}

public sealed class AuthService : IAuthService
{
    private readonly FiloDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;
    private readonly IDateTimeProvider _clock;
    private readonly SecurityOptions _sec;

    public AuthService(FiloDbContext db, IPasswordHasher hasher, ITokenService tokens,
        IDateTimeProvider clock, IOptions<SecurityOptions> sec)
    {
        _db = db; _hasher = hasher; _tokens = tokens; _clock = clock; _sec = sec.Value;
    }

    public async Task<Result<LoginResponse>> LoginAsync(
        LoginRequest req, string? ip, string? userAgent, string? correlationId, CancellationToken ct)
    {
        var email = (req.Email ?? "").Trim();
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == email, ct);

        // Kullanıcı bulunamasa bile aynı hata + audit (kullanıcı sayımı sızmasın).
        async Task Audit(int? uid, bool ok, string? reason)
        {
            _db.LoginAuditLogs.Add(new LoginAuditLog
            {
                UserId = uid, AttemptedEmail = email, Succeeded = ok, FailureReason = reason,
                IpAddress = ip, UserAgent = userAgent, CorrelationId = correlationId,
                OccurredAtUtc = _clock.UtcNow
            });
            await _db.SaveChangesAsync(ct);
        }

        if (user is null)
        {
            await Audit(null, false, LoginFailureReasons.InvalidCredentials);
            return Result<LoginResponse>.Fail("E-posta veya parola hatalı.", ResultCode.Unauthorized);
        }

        // Kilitli mi?
        if (user.LockoutEndUtc is { } until && until > _clock.UtcNow)
        {
            await Audit(user.Id, false, LoginFailureReasons.LockedOut);
            return Result<LoginResponse>.Fail(
                $"Hesap geçici olarak kilitli. {Math.Ceiling((until - _clock.UtcNow).TotalMinutes)} dk sonra deneyin.",
                ResultCode.Forbidden);
        }

        if (!user.IsActive)
        {
            await Audit(user.Id, false, LoginFailureReasons.Inactive);
            return Result<LoginResponse>.Fail("Hesap pasif. Yöneticinize başvurun.", ResultCode.Forbidden);
        }

        // Parola atanmamış (seed'den gelen kabuk hesap) → giriş yapılamaz.
        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            await Audit(user.Id, false, LoginFailureReasons.NoPassword);
            return Result<LoginResponse>.Fail("Bu hesaba henüz parola atanmamış.", ResultCode.Forbidden);
        }

        if (!_hasher.Verify(req.Password ?? "", user.PasswordHash, out var rehash))
        {
            user.AccessFailedCount++;
            if (user.AccessFailedCount >= _sec.MaxFailedAccessAttempts)
            {
                user.LockoutEndUtc = _clock.UtcNow.AddMinutes(_sec.LockoutMinutes);
                user.AccessFailedCount = 0;
            }
            await Audit(user.Id, false, LoginFailureReasons.InvalidCredentials);
            return Result<LoginResponse>.Fail("E-posta veya parola hatalı.", ResultCode.Unauthorized);
        }

        // Başarılı giriş.
        user.AccessFailedCount = 0;
        user.LockoutEndUtc = null;
        user.LastLoginAtUtc = _clock.UtcNow;
        if (rehash) user.PasswordHash = _hasher.Hash(req.Password!); // tur sayısı arttıysa güncelle

        var resp = await IssueTokensAsync(user, ip, ct);
        await Audit(user.Id, true, null);
        return Result<LoginResponse>.Success(resp);
    }

    public async Task<Result<LoginResponse>> RefreshAsync(RefreshRequest req, string? ip, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.RefreshToken))
            return Result<LoginResponse>.Fail("Refresh token gerekli.", ResultCode.Unauthorized);

        var hash = _tokens.HashRefreshToken(req.RefreshToken);
        var token = await _db.RefreshTokens
            .Include(t => t.User).ThenInclude(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (token is null || !token.IsActive(_clock.UtcNow))
            return Result<LoginResponse>.Fail("Refresh token geçersiz veya süresi dolmuş.", ResultCode.Unauthorized);

        var user = token.User;
        if (!user.IsActive)
            return Result<LoginResponse>.Fail("Hesap pasif.", ResultCode.Forbidden);

        // ROTASYON: eski token iptal, yenisi üretilir, zincir bağlanır.
        token.RevokedAtUtc = _clock.UtcNow;
        token.RevokedReason = "rotated";

        var resp = await IssueTokensAsync(user, ip, ct, replacingTokenId: token.Id);
        await _db.SaveChangesAsync(ct);
        return Result<LoginResponse>.Success(resp);
    }

    public async Task<Result> LogoutAsync(string refreshToken, string? ip, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return Result.Success();
        var hash = _tokens.HashRefreshToken(refreshToken);
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is { RevokedAtUtc: null })
        {
            token.RevokedAtUtc = _clock.UtcNow;
            token.RevokedReason = "logout";
            await _db.SaveChangesAsync(ct);
        }
        return Result.Success();
    }

    public async Task<Result> ChangePasswordAsync(int userId, ChangePasswordRequest req, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Result.Fail("Kullanıcı bulunamadı.", ResultCode.NotFound);

        if (string.IsNullOrEmpty(user.PasswordHash) ||
            !_hasher.Verify(req.CurrentPassword ?? "", user.PasswordHash, out _))
            return Result.Fail("Mevcut parola hatalı.", ResultCode.Unauthorized);

        var err = PasswordPolicy.Validate(req.NewPassword, _sec.MinPasswordLength);
        if (err is not null) return Result.Fail(err, ResultCode.Validation);

        user.PasswordHash = _hasher.Hash(req.NewPassword!);
        user.MustChangePassword = false;
        user.SecurityStamp = Guid.NewGuid();

        // Parola değişince TÜM refresh token'ları iptal (diğer oturumlar düşsün).
        var active = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null).ToListAsync(ct);
        foreach (var t in active) { t.RevokedAtUtc = _clock.UtcNow; t.RevokedReason = "password_changed"; }

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<UserInfo>> GetMeAsync(int userId, CancellationToken ct)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Result<UserInfo>.Fail("Kullanıcı bulunamadı.", ResultCode.NotFound);

        var (roles, perms) = await LoadRolesAndPermsAsync(user, ct);
        return Result<UserInfo>.Success(new UserInfo(
            user.Id, user.Email, user.FullName, user.MustChangePassword, roles, perms));
    }

    // ---- yardımcılar ----
    private async Task<LoginResponse> IssueTokensAsync(User user, string? ip, CancellationToken ct, long? replacingTokenId = null)
    {
        var (roles, perms) = await LoadRolesAndPermsAsync(user, ct);
        var (access, accessExp) = _tokens.CreateAccessToken(user.Id, user.Email, roles, perms);
        var (raw, hash, refreshExp) = _tokens.CreateRefreshToken();

        var rt = new RefreshToken
        {
            UserId = user.Id, TokenHash = hash, ExpiresAtUtc = refreshExp,
            CreatedAtUtc = _clock.UtcNow, CreatedByIp = ip
        };
        _db.RefreshTokens.Add(rt);
        await _db.SaveChangesAsync(ct); // rt.Id oluşsun

        if (replacingTokenId is { } oldId)
        {
            var old = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.Id == oldId, ct);
            if (old is not null) old.ReplacedById = rt.Id;
        }

        return new LoginResponse(access, raw, accessExp,
            new UserInfo(user.Id, user.Email, user.FullName, user.MustChangePassword, roles, perms));
    }

    private async Task<(IReadOnlyCollection<string> roles, IReadOnlyCollection<string> perms)>
        LoadRolesAndPermsAsync(User user, CancellationToken ct)
    {
        var roleIds = user.UserRoles.Select(ur => ur.RoleId).ToList();
        var roles = user.UserRoles.Select(ur => ur.Role.Key).Distinct().ToList();
        var perms = await _db.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => rp.Permission.Key)
            .Distinct()
            .ToListAsync(ct);
        return (roles, perms);
    }
}
