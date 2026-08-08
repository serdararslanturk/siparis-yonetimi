namespace Acr.Filo.Application.Abstractions;

/// <summary>İstek başına oturum açan kullanıcı. Api katmanı HttpContext'ten doldurur.</summary>
public interface ICurrentUser
{
    int? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    IReadOnlyCollection<string> Permissions { get; }
    IReadOnlyCollection<string> Roles { get; }
    bool Has(string permission);
}
