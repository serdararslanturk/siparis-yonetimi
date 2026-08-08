namespace Acr.Filo.Domain.Entities.Auth;

/// <summary>SQL: dbo.LoginAuditLogs. Parola ASLA yazılmaz — yalnız sonuç ve sebep.</summary>
public class LoginAuditLog
{
    public long Id { get; set; }
    public int? UserId { get; set; }
    public string? AttemptedEmail { get; set; }
    public bool Succeeded { get; set; }
    public string? FailureReason { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime OccurredAtUtc { get; set; }

    public User? User { get; set; }
}

public static class LoginFailureReasons
{
    public const string InvalidCredentials = "invalid_credentials";
    public const string LockedOut          = "locked_out";
    public const string Inactive           = "inactive";
    public const string NoPassword         = "no_password";
}
