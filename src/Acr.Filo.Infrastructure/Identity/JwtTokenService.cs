using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Acr.Filo.Application.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Acr.Filo.Infrastructure.Identity;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "acr-filo";
    public string Audience { get; set; } = "acr-filo-web";
    public string SigningKey { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 14;
}

public sealed class JwtTokenService : ITokenService
{
    private readonly JwtOptions _opt;
    private readonly SymmetricSecurityKey _key;

    public JwtTokenService(IOptions<JwtOptions> opt)
    {
        _opt = opt.Value;
        if (string.IsNullOrWhiteSpace(_opt.SigningKey) ||
            Encoding.UTF8.GetByteCount(_opt.SigningKey) < 32)
            throw new InvalidOperationException(
                "Jwt:SigningKey eksik veya 32 byte'tan kısa. appsettings.Production.json veya ENV Jwt__SigningKey ayarlayın.");
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.SigningKey));
    }

    public (string token, DateTime expiresUtc) CreateAccessToken(
        int userId, string email, IEnumerable<string> roles, IEnumerable<string> permissions)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_opt.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };
        foreach (var r in roles) claims.Add(new Claim(ClaimTypes.Role, r));
        // Yetkiler ayrı claim tipinde — policy bunlara bakar.
        foreach (var p in permissions) claims.Add(new Claim("perm", p));

        var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(_opt.Issuer, _opt.Audience, claims, now, expires, creds);
        return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }

    public (string raw, byte[] hash, DateTime expiresUtc) CreateRefreshToken()
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var hash = HashRefreshToken(raw);
        return (raw, hash, DateTime.UtcNow.AddDays(_opt.RefreshTokenDays));
    }

    public byte[] HashRefreshToken(string raw)
        => SHA256.HashData(Encoding.UTF8.GetBytes(raw));
}
