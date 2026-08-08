using Acr.Filo.Application.Common;
using Acr.Filo.Application.Users;
using Acr.Filo.Domain.Entities.Auth;
using Acr.Filo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Acr.Filo.Infrastructure.Services;

public sealed class UserService : IUserService
{
    private readonly FiloDbContext _db;
    public UserService(FiloDbContext db) => _db = db;

    public async Task<Result<PagedResult<UserListDto>>> ListAsync(PageQuery q, CancellationToken ct)
    {
        var query = _db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            query = query.Where(u => u.Email.Contains(s) || u.FullName.Contains(s));
        }
        var total = await query.CountAsync(ct);
        var users = await query.OrderBy(u => u.FullName).Skip(q.Skip).Take(q.PageSize).ToListAsync(ct);
        var items = users.Select(u => new UserListDto(u.Id, u.Email, u.FullName, u.IsActive,
            u.UserRoles.Select(r => r.Role.Key).ToList(), u.LastLoginAtUtc)).ToList();
        return Result<PagedResult<UserListDto>>.Success(new PagedResult<UserListDto>
        { Items = items, Total = total, Page = q.Page, PageSize = q.PageSize });
    }

    public async Task<Result<UserListDto>> CreateAsync(CreateUserRequest req, CancellationToken ct)
    {
        var email = (req.Email ?? "").Trim();
        if (email.Length == 0) return Result<UserListDto>.Fail("E-posta gerekli.", ResultCode.Validation);
        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            return Result<UserListDto>.Fail("Bu e-posta zaten kayıtlı.", ResultCode.Conflict);

        var roles = await ResolveRoles(req.Roles, ct);
        if (roles is null) return Result<UserListDto>.Fail("Geçersiz rol.", ResultCode.Validation);

        // Parola YOK, hesap PASİF oluşturulur (seed'deki admin gibi). Parola --set-admin-password
        // benzeri akışla veya "parola sıfırla" ile atanır. Açık metin parola üretilmez.
        var u = new User { Email = email, FullName = (req.FullName ?? "").Trim(), IsActive = false, MustChangePassword = true };
        foreach (var r in roles) u.UserRoles.Add(new UserRole { Role = r });
        _db.Users.Add(u);
        await _db.SaveChangesAsync(ct);
        return Result<UserListDto>.Success(new UserListDto(u.Id, u.Email, u.FullName, u.IsActive,
            roles.Select(r => r.Key).ToList(), null));
    }

    public async Task<Result<UserListDto>> UpdateAsync(int id, UpdateUserRequest req, CancellationToken ct)
    {
        var u = await _db.Users.Include(x => x.UserRoles).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (u is null) return Result<UserListDto>.Fail("Kullanıcı bulunamadı.", ResultCode.NotFound);

        var roles = await ResolveRoles(req.Roles, ct);
        if (roles is null) return Result<UserListDto>.Fail("Geçersiz rol.", ResultCode.Validation);

        u.FullName = (req.FullName ?? "").Trim();
        u.IsActive = req.IsActive;
        u.UserRoles.Clear();
        foreach (var r in roles) u.UserRoles.Add(new UserRole { RoleId = r.Id });
        _db.Entry(u).Property(x => x.RowVersion).OriginalValue = req.RowVersion;

        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        { return Result<UserListDto>.Fail("Kayıt başka kullanıcı tarafından değiştirilmiş.", ResultCode.Conflict); }

        return Result<UserListDto>.Success(new UserListDto(u.Id, u.Email, u.FullName, u.IsActive,
            roles.Select(r => r.Key).ToList(), u.LastLoginAtUtc));
    }

    public async Task<Result> DeactivateAsync(int id, CancellationToken ct)
    {
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (u is null) return Result.Fail("Kullanıcı bulunamadı.", ResultCode.NotFound);
        u.IsActive = false;
        // Aktif refresh token'ları iptal.
        var tokens = await _db.RefreshTokens.Where(t => t.UserId == id && t.RevokedAtUtc == null).ToListAsync(ct);
        foreach (var t in tokens) { t.RevokedAtUtc = DateTime.UtcNow; t.RevokedReason = "user_deactivated"; }
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<RoleDto>>> RolesAsync(CancellationToken ct)
    {
        var roles = await _db.Roles.Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .OrderBy(r => r.Id).ToListAsync(ct);
        var dtos = roles.Select(r => new RoleDto(r.Id, r.Key, r.Name,
            r.RolePermissions.Select(rp => rp.Permission.Key).ToList())).ToList();
        return Result<IReadOnlyList<RoleDto>>.Success(dtos);
    }

    private async Task<List<Role>?> ResolveRoles(IReadOnlyCollection<string> keys, CancellationToken ct)
    {
        if (keys is null || keys.Count == 0) return new List<Role>();
        var roles = await _db.Roles.Where(r => keys.Contains(r.Key)).ToListAsync(ct);
        return roles.Count == keys.Distinct().Count() ? roles : null;
    }
}
