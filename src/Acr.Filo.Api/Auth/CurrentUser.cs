using System.Security.Claims;
using Acr.Filo.Application.Abstractions;

namespace Acr.Filo.Api.Auth;

public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _http;
    public CurrentUser(IHttpContextAccessor http) => _http = http;

    private ClaimsPrincipal? User => _http.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public int? UserId
    {
        get
        {
            var sub = User?.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
                      ?? User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(sub, out var id) ? id : null;
        }
    }

    public string? Email => User?.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)
                            ?? User?.FindFirstValue(ClaimTypes.Email);

    public IReadOnlyCollection<string> Permissions =>
        User?.FindAll("perm").Select(c => c.Value).ToArray() ?? Array.Empty<string>();

    public IReadOnlyCollection<string> Roles =>
        User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? Array.Empty<string>();

    public bool Has(string permission) => Permissions.Contains(permission);
}
