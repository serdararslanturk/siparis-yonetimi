using Acr.Filo.Application.Abstractions;
using Acr.Filo.Domain.Common;
using Acr.Filo.Domain.Entities.System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Acr.Filo.Infrastructure.Auditing;

/// <summary>
/// SaveChanges sırasında audit üretir + CreatedAt/UpdatedAt/CreatedBy/UpdatedBy doldurur.
/// Şartname madde 15: kim, ne zaman, hangi alanı değiştirdi; eski/yeni değer.
///
/// Loglanmayan alanlar (şartname: şifre/token loglanmaz): PasswordHash, SecurityStamp,
/// TokenHash, RowVersion. Bunlar audit'e YAZILMAZ.
/// </summary>
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUser _current;
    private readonly IDateTimeProvider _clock;

    private static readonly HashSet<string> Skip = new(StringComparer.Ordinal)
    {
        "PasswordHash", "SecurityStamp", "TokenHash", "RowVersion",
        "CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy"
    };
    // Bu entity'ler audit üretmez (audit'in kendisi + login logu + refresh token gürültüsü).
    private static readonly HashSet<string> SkipEntities = new(StringComparer.Ordinal)
    {
        nameof(AuditLog), "LoginAuditLog", "RefreshToken"
    };

    public AuditSaveChangesInterceptor(ICurrentUser current, IDateTimeProvider clock)
    {
        _current = current; _clock = clock;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        var ctx = eventData.Context;
        if (ctx is not null) Apply(ctx);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        var ctx = eventData.Context;
        if (ctx is not null) Apply(ctx);
        return base.SavingChanges(eventData, result);
    }

    private void Apply(DbContext ctx)
    {
        var now = _clock.UtcNow;
        var uid = _current.UserId;
        var logs = new List<AuditLog>();

        foreach (var entry in ctx.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is AuditLog) continue;
            var entityName = entry.Metadata.ClrType.Name;

            // Denetim alanlarını otomatik doldur.
            if (entry.Entity is AuditableEntity aud)
            {
                if (entry.State == EntityState.Added)
                {
                    aud.CreatedAt = now; aud.CreatedBy = uid;
                }
                else if (entry.State == EntityState.Modified)
                {
                    aud.UpdatedAt = now; aud.UpdatedBy = uid;
                    entry.Property(nameof(AuditableEntity.CreatedAt)).IsModified = false;
                    entry.Property(nameof(AuditableEntity.CreatedBy)).IsModified = false;
                }
            }

            if (SkipEntities.Contains(entityName)) continue;
            var id = GetKey(entry);

            if (entry.State == EntityState.Added)
            {
                logs.Add(Make(entityName, id, "Insert", null, null, null, uid, now));
            }
            else if (entry.State == EntityState.Deleted)
            {
                logs.Add(Make(entityName, id, "Delete", null, null, null, uid, now));
            }
            else if (entry.State == EntityState.Modified)
            {
                foreach (var p in entry.Properties)
                {
                    if (!p.IsModified) continue;
                    if (Skip.Contains(p.Metadata.Name)) continue;
                    var oldV = p.OriginalValue?.ToString();
                    var newV = p.CurrentValue?.ToString();
                    if (oldV == newV) continue;
                    logs.Add(Make(entityName, id, "Update", p.Metadata.Name, oldV, newV, uid, now));
                }
            }
        }

        if (logs.Count > 0) ctx.Set<AuditLog>().AddRange(logs);
    }

    private static string GetKey(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null) return "?";
        var vals = key.Properties.Select(p => entry.Property(p.Name).CurrentValue?.ToString());
        return string.Join(",", vals);
    }

    private static AuditLog Make(string entity, string id, string action, string? col,
        string? oldV, string? newV, int? uid, DateTime now) => new()
    {
        EntityName = entity, EntityId = id, Action = action, ColumnName = col,
        OldValue = oldV, NewValue = newV, UserId = uid, OccurredAtUtc = now
    };
}
