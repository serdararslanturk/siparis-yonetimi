using Acr.Filo.Application.Common;
using Acr.Filo.Application.Definitions;
using Acr.Filo.Domain.Common;
using Acr.Filo.Domain.Entities.Definitions;
using Acr.Filo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Acr.Filo.Infrastructure.Services;

public sealed class DefinitionService : IDefinitionService
{
    private readonly FiloDbContext _db;
    public DefinitionService(FiloDbContext db) => _db = db;

    // Tür -> tablo eşlemesi tek yerde. Geçersiz tür 404.
    private static bool Valid(string tur) => tur is "customers" or "suppliers" or "brands";

    public async Task<Result<PagedResult<DefinitionDto>>> ListAsync(string tur, PageQuery q, CancellationToken ct)
    {
        if (!Valid(tur)) return Result<PagedResult<DefinitionDto>>.Fail("Geçersiz tanım türü.", ResultCode.NotFound);
        var (items, total) = tur switch
        {
            "customers" => await PageAsync(_db.Customers, c => new DefinitionDto(c.Id, c.Unvan, c.IsActive, c.RowVersion), q, ct),
            "suppliers" => await PageAsync(_db.Suppliers, s => new DefinitionDto(s.Id, s.Unvan, s.IsActive, s.RowVersion), q, ct),
            _           => await PageAsync(_db.Brands,    b => new DefinitionDto(b.Id, b.Ad,    b.IsActive, b.RowVersion), q, ct),
        };
        return Result<PagedResult<DefinitionDto>>.Success(new PagedResult<DefinitionDto>
        { Items = items, Total = total, Page = q.Page, PageSize = q.PageSize });
    }

    public async Task<Result<IReadOnlyList<DefinitionDto>>> AllActiveAsync(string tur, CancellationToken ct)
    {
        if (!Valid(tur)) return Result<IReadOnlyList<DefinitionDto>>.Fail("Geçersiz tanım türü.", ResultCode.NotFound);
        IReadOnlyList<DefinitionDto> list = tur switch
        {
            "customers" => await _db.Customers.Where(c => c.IsActive).OrderBy(c => c.Unvan)
                .Select(c => new DefinitionDto(c.Id, c.Unvan, c.IsActive, c.RowVersion)).ToListAsync(ct),
            "suppliers" => await _db.Suppliers.Where(s => s.IsActive).OrderBy(s => s.Unvan)
                .Select(s => new DefinitionDto(s.Id, s.Unvan, s.IsActive, s.RowVersion)).ToListAsync(ct),
            _           => await _db.Brands.Where(b => b.IsActive).OrderBy(b => b.Ad)
                .Select(b => new DefinitionDto(b.Id, b.Ad, b.IsActive, b.RowVersion)).ToListAsync(ct),
        };
        return Result<IReadOnlyList<DefinitionDto>>.Success(list);
    }

    public async Task<Result<DefinitionDto>> CreateAsync(string tur, CreateDefinitionRequest req, CancellationToken ct)
    {
        if (!Valid(tur)) return Result<DefinitionDto>.Fail("Geçersiz tanım türü.", ResultCode.NotFound);
        var ad = (req.Ad ?? "").Trim();
        if (ad.Length == 0) return Result<DefinitionDto>.Fail("Ad boş olamaz.", ResultCode.Validation);
        try
        {
            DefinitionDto dto;
            if (tur == "customers") { var e = new Customer { Unvan = ad }; _db.Customers.Add(e); await _db.SaveChangesAsync(ct); dto = new(e.Id, e.Unvan, e.IsActive, e.RowVersion); }
            else if (tur == "suppliers") { var e = new Supplier { Unvan = ad }; _db.Suppliers.Add(e); await _db.SaveChangesAsync(ct); dto = new(e.Id, e.Unvan, e.IsActive, e.RowVersion); }
            else { var e = new Brand { Ad = ad }; _db.Brands.Add(e); await _db.SaveChangesAsync(ct); dto = new(e.Id, e.Ad, e.IsActive, e.RowVersion); }
            return Result<DefinitionDto>.Success(dto);
        }
        catch (DbUpdateException) // filtered unique ihlali
        {
            return Result<DefinitionDto>.Fail("Bu ad zaten kayıtlı.", ResultCode.Conflict);
        }
    }

    public async Task<Result<DefinitionDto>> UpdateAsync(string tur, int id, UpdateDefinitionRequest req, CancellationToken ct)
    {
        if (!Valid(tur)) return Result<DefinitionDto>.Fail("Geçersiz tanım türü.", ResultCode.NotFound);
        var ad = (req.Ad ?? "").Trim();
        if (ad.Length == 0) return Result<DefinitionDto>.Fail("Ad boş olamaz.", ResultCode.Validation);
        try
        {
            if (tur == "customers")
            {
                var e = await _db.Customers.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (e is null) return NotFound();
                e.Unvan = ad; e.IsActive = req.IsActive;
                _db.Entry(e).Property(x => x.RowVersion).OriginalValue = req.RowVersion;
                await _db.SaveChangesAsync(ct);
                return Result<DefinitionDto>.Success(new(e.Id, e.Unvan, e.IsActive, e.RowVersion));
            }
            if (tur == "suppliers")
            {
                var e = await _db.Suppliers.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (e is null) return NotFound();
                e.Unvan = ad; e.IsActive = req.IsActive;
                _db.Entry(e).Property(x => x.RowVersion).OriginalValue = req.RowVersion;
                await _db.SaveChangesAsync(ct);
                return Result<DefinitionDto>.Success(new(e.Id, e.Unvan, e.IsActive, e.RowVersion));
            }
            {
                var e = await _db.Brands.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (e is null) return NotFound();
                e.Ad = ad; e.IsActive = req.IsActive;
                _db.Entry(e).Property(x => x.RowVersion).OriginalValue = req.RowVersion;
                await _db.SaveChangesAsync(ct);
                return Result<DefinitionDto>.Success(new(e.Id, e.Ad, e.IsActive, e.RowVersion));
            }
        }
        catch (DbUpdateConcurrencyException)
        { return Result<DefinitionDto>.Fail("Kayıt başka kullanıcı tarafından değiştirilmiş.", ResultCode.Conflict); }
        catch (DbUpdateException)
        { return Result<DefinitionDto>.Fail("Bu ad zaten kayıtlı.", ResultCode.Conflict); }

        Result<DefinitionDto> NotFound() => Result<DefinitionDto>.Fail("Kayıt bulunamadı.", ResultCode.NotFound);
    }

    public async Task<Result> DeleteAsync(string tur, int id, CancellationToken ct)
    {
        if (!Valid(tur)) return Result.Fail("Geçersiz tanım türü.", ResultCode.NotFound);
        // Soft-delete. Kullanımdaki tanım FK yüzünden silinemez -> yalnız pasifleştirilir.
        ISoftDeletable? e = tur switch
        {
            "customers" => await _db.Customers.FirstOrDefaultAsync(x => x.Id == id, ct),
            "suppliers" => await _db.Suppliers.FirstOrDefaultAsync(x => x.Id == id, ct),
            _           => await _db.Brands.FirstOrDefaultAsync(x => x.Id == id, ct),
        };
        if (e is null) return Result.Fail("Kayıt bulunamadı.", ResultCode.NotFound);
        e.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static async Task<(List<DefinitionDto>, int)> PageAsync<TEntity>(
        IQueryable<TEntity> set, System.Linq.Expressions.Expression<Func<TEntity, DefinitionDto>> map,
        PageQuery q, CancellationToken ct) where TEntity : class
    {
        var query = set.AsNoTracking();
        var total = await query.CountAsync(ct);
        var items = await query.Select(map)
            .Where(d => string.IsNullOrEmpty(q.Search) || d.Ad.Contains(q.Search!))
            .OrderBy(d => d.Ad).Skip(q.Skip).Take(q.PageSize).ToListAsync(ct);
        return (items, total);
    }
}
