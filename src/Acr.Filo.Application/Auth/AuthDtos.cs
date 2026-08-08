namespace Acr.Filo.Application.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresUtc,
    UserInfo User);

public sealed record RefreshRequest(string RefreshToken);

public sealed record UserInfo(
    int Id,
    string Email,
    string FullName,
    bool MustChangePassword,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
