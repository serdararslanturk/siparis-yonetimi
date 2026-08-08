using Acr.Filo.Domain.Entities.Auth;

namespace Acr.Filo.Domain.Entities.System;

/// <summary>
/// SQL: dbo.AuditLogs. Kolon bazında eski/yeni değer tutar.
/// DB tarafında UPDATE/DELETE hem tetikleyici hem DENY ile engellidir.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }
    public string EntityName { get; set; } = null!;
    public string EntityId { get; set; } = null!;
    public string Action { get; set; } = null!;
    public string? ColumnName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public int? UserId { get; set; }
    public string? CorrelationId { get; set; }
    public string? IpAddress { get; set; }
    public DateTime OccurredAtUtc { get; set; }

    public User? User { get; set; }
}

public static class AuditActions
{
    public const string Insert = "Insert";
    public const string Update = "Update";
    public const string Delete = "Delete";
}
