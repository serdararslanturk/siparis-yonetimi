using Acr.Filo.Application.Abstractions;
using Acr.Filo.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Acr.Filo.Api.Controllers;

public sealed class AuthController : ApiControllerBase
{
    private readonly IAuthService _auth;
    private readonly ICurrentUser _current;
    public AuthController(IAuthService auth, ICurrentUser current) { _auth = auth; _current = current; }

    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();
    private string? Ua => Request.Headers.UserAgent.ToString();

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
        => ToResponse(await _auth.LoginAsync(req, Ip, Ua, HttpContext.TraceIdentifier, ct));

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req, CancellationToken ct)
        => ToResponse(await _auth.RefreshAsync(req, Ip, ct));

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest req, CancellationToken ct)
        => ToResponse(await _auth.LogoutAsync(req.RefreshToken, Ip, ct));

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        if (_current.UserId is not { } uid) return Fail("Oturum bulunamadı.", 401);
        return ToResponse(await _auth.GetMeAsync(uid, ct));
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req, CancellationToken ct)
    {
        if (_current.UserId is not { } uid) return Fail("Oturum bulunamadı.", 401);
        return ToResponse(await _auth.ChangePasswordAsync(uid, req, ct));
    }
}
