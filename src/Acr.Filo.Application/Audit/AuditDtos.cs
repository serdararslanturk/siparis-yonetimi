using Acr.Filo.Application.Common;

namespace Acr.Filo.Application.Audit;

public sealed record AuditLogDto(long Id, string EntityName, string EntityId, string Action,
    string? ColumnName, string? OldValue, string? NewValue, int? UserId, string? UserEmail,
    string? IpAddress, DateTime OccurredAtUtc);

public sealed class AuditQuery : PageQuery
{
    public string? EntityName { get; set; }
    public string? EntityId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public interface IAuditService
{
    Task<Result<PagedResult<AuditLogDto>>> ListAsync(AuditQuery q, CancellationToken ct);
}

/// <summary>DbContext SaveChanges sırasında audit üretir. Infrastructure uygular.</summary>
public interface IAuditWriter
{
    // İşaretleyici; gerçek yazım Infrastructure interceptor'ında.
}
