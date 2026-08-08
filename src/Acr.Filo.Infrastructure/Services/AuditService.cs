using Acr.Filo.Application.Audit;
using Acr.Filo.Application.Common;
using Acr.Filo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Acr.Filo.Infrastructure.Services;

public sealed class AuditService : IAuditService
{
    private readonly FiloDbContext _db;
    public AuditService(FiloDbContext db) => _db = db;

    public async Task<Result<PagedResult<AuditLogDto>>> ListAsync(AuditQuery q, CancellationToken ct)
    {
        var query = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.EntityName)) query = query.Where(a => a.EntityName == q.EntityName);
        if (!string.IsNullOrWhiteSpace(q.EntityId)) query = query.Where(a => a.EntityId == q.EntityId);
        if (q.From is { } f) query = query.Where(a => a.OccurredAtUtc >= f.ToDateTime(TimeOnly.MinValue));
        if (q.To is { } t) query = query.Where(a => a.OccurredAtUtc < t.AddDays(1).ToDateTime(TimeOnly.MinValue));

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(a => a.OccurredAtUtc)
            .Skip(q.Skip).Take(q.PageSize)
            .Join(_db.Users.IgnoreQueryFilters(), a => a.UserId, u => (int?)u.Id,
                (a, u) => new { a, email = u.Email })
            .ToListAsync(ct);

        // Kullanıcısı olmayan (sistem) kayıtlar için join dışı kalanları da al:
        var items = rows.Select(x => new AuditLogDto(x.a.Id, x.a.EntityName, x.a.EntityId, x.a.Action,
            x.a.ColumnName, x.a.OldValue, x.a.NewValue, x.a.UserId, x.email, x.a.IpAddress, x.a.OccurredAtUtc)).ToList();

        return Result<PagedResult<AuditLogDto>>.Success(new PagedResult<AuditLogDto>
        { Items = items, Total = total, Page = q.Page, PageSize = q.PageSize });
    }
}
