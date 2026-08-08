using Acr.Filo.Application.Auth;
using Acr.Filo.Domain.Entities.Auth;
using Acr.Filo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acr.Filo.Api;

/// <summary>
/// `dotnet Acr.Filo.Api.dll --set-admin-password`
/// Konsoldan maskeli parola okur, PBKDF2 ile hashler, ilk admin hesabını aktifleştirir.
/// Parola hiçbir yere yazılmaz, loglanmaz. Şartname madde 6.
/// </summary>
public static class AdminPasswordTool
{
    public static async Task RunAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FiloDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var admin = await db.Users.Include(u => u.UserRoles).ThenInclude(r => r.Role)
            .Where(u => u.UserRoles.Any(r => r.Role.Key == RoleKeys.Admin))
            .OrderBy(u => u.Id)
            .FirstOrDefaultAsync();

        if (admin is null)
        {
            Console.WriteLine("HATA: admin rolünde kullanıcı bulunamadı. Önce db/03-seed.sql çalıştırın.");
            return;
        }

        Console.WriteLine($"Yönetici hesabı: {admin.Email}");
        Console.Write("Yeni parola: ");
        var p1 = ReadHidden();
        Console.Write("Parola (tekrar): ");
        var p2 = ReadHidden();

        if (p1 != p2) { Console.WriteLine("Parolalar eşleşmiyor."); return; }
        var err = PasswordPolicy.Validate(p1, 12);
        if (err is not null) { Console.WriteLine("HATA: " + err); return; }

        admin.PasswordHash = hasher.Hash(p1);
        admin.IsActive = true;
        admin.MustChangePassword = false;
        admin.SecurityStamp = Guid.NewGuid();
        await db.SaveChangesAsync();
        Console.WriteLine("Yönetici parolası atandı ve hesap aktifleştirildi.");
    }

    /// <summary>
    /// `dotnet Acr.Filo.Api.dll --add-user`
    /// Konsoldan e-posta, ad, rol ve maskeli parola okur; yeni kullanıcı oluşturup
    /// aktifleştirir. Kullanıcı zaten varsa parolasını ve rollerini günceller.
    /// </summary>
    public static async Task AddUserAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FiloDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        Console.WriteLine("=== Yeni Kullanıcı Ekle ===");

        // E-posta
        Console.Write("E-posta: ");
        var email = (Console.ReadLine() ?? "").Trim();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            Console.WriteLine("HATA: Geçerli bir e-posta girin.");
            return;
        }

        // Ad Soyad
        Console.Write("Ad Soyad: ");
        var fullName = (Console.ReadLine() ?? "").Trim();
        if (string.IsNullOrWhiteSpace(fullName))
        {
            Console.WriteLine("HATA: Ad Soyad boş olamaz.");
            return;
        }

        // Rol seçimi
        Console.WriteLine("Roller: [1] admin  [2] operasyon  [3] muhasebe");
        Console.Write("Rol numarası (birden fazla için virgülle, örn 2,3): ");
        var roleInput = (Console.ReadLine() ?? "").Trim();
        var roleKeys = new List<string>();
        foreach (var part in roleInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (part)
            {
                case "1": roleKeys.Add(RoleKeys.Admin); break;
                case "2": roleKeys.Add(RoleKeys.Operasyon); break;
                case "3": roleKeys.Add(RoleKeys.Muhasebe); break;
                default: Console.WriteLine($"HATA: Geçersiz rol numarası '{part}'."); return;
            }
        }
        if (roleKeys.Count == 0)
        {
            Console.WriteLine("HATA: En az bir rol seçin.");
            return;
        }

        // Rolleri veritabanından bul
        var roles = await db.Set<Role>().Where(r => roleKeys.Contains(r.Key)).ToListAsync();
        if (roles.Count != roleKeys.Distinct().Count())
        {
            Console.WriteLine("HATA: Bazı roller veritabanında bulunamadı. Önce db/03-seed.sql çalıştırın.");
            return;
        }

        // Parola
        Console.Write("Parola: ");
        var p1 = ReadHidden();
        Console.Write("Parola (tekrar): ");
        var p2 = ReadHidden();
        if (p1 != p2) { Console.WriteLine("HATA: Parolalar eşleşmiyor."); return; }
        // --add-user icin parola politikasi uygulanmaz.
        if (string.IsNullOrEmpty(p1)) { Console.WriteLine("HATA: Parola bos olamaz."); return; }

        // Kullanıcı var mı?
        var existing = await db.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Email == email);

        if (existing is not null)
        {
            Console.Write($"'{email}' zaten var. Parola ve rolleri güncellensin mi? (E/H): ");
            var confirm = (Console.ReadLine() ?? "").Trim().ToUpperInvariant();
            if (confirm != "E" && confirm != "EVET") { Console.WriteLine("İptal edildi."); return; }

            existing.FullName = fullName;
            existing.PasswordHash = hasher.Hash(p1);
            existing.IsActive = true;
            existing.IsDeleted = false;
            existing.MustChangePassword = false;
            existing.SecurityStamp = Guid.NewGuid();
            existing.AccessFailedCount = 0;
            existing.LockoutEndUtc = null;

            // rolleri sıfırla, yenilerini ata
            existing.UserRoles.Clear();
            foreach (var role in roles)
                existing.UserRoles.Add(new UserRole { UserId = existing.Id, RoleId = role.Id });

            await db.SaveChangesAsync();
            Console.WriteLine($"Kullanıcı güncellendi: {email} — roller: {string.Join(", ", roleKeys)}");
            return;
        }

        // Yeni kullanıcı
        var user = new User
        {
            Email = email,
            FullName = fullName,
            PasswordHash = hasher.Hash(p1),
            IsActive = true,
            IsDeleted = false,
            MustChangePassword = false,
            SecurityStamp = Guid.NewGuid(),
        };
        foreach (var role in roles)
            user.UserRoles.Add(new UserRole { Role = role });

        db.Users.Add(user);
        await db.SaveChangesAsync();
        Console.WriteLine($"Kullanıcı oluşturuldu ve aktifleştirildi: {email} — roller: {string.Join(", ", roleKeys)}");
    }

    /// <summary>
    /// `dotnet Acr.Filo.Api.dll --list-users`
    /// Kullanıcıları ve rollerini listeler (parola göstermez).
    /// </summary>
    public static async Task ListUsersAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FiloDbContext>();
        var users = await db.Users
            .Include(u => u.UserRoles).ThenInclude(r => r.Role)
            .OrderBy(u => u.Id)
            .ToListAsync();

        Console.WriteLine("=== Kullanıcılar ===");
        Console.WriteLine($"{"Id",-4} {"E-posta",-32} {"Ad",-24} {"Aktif",-6} Roller");
        foreach (var u in users)
        {
            var roles = string.Join(",", u.UserRoles.Select(r => r.Role.Key));
            var aktif = u.IsActive ? "Evet" : "Hayır";
            Console.WriteLine($"{u.Id,-4} {u.Email,-32} {u.FullName,-24} {aktif,-6} {roles}");
        }
    }

    private static string ReadHidden()
    {
        var sb = new System.Text.StringBuilder();
        ConsoleKeyInfo key;
        while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
        {
            if (key.Key == ConsoleKey.Backspace) { if (sb.Length > 0) sb.Length--; }
            else if (!char.IsControl(key.KeyChar)) sb.Append(key.KeyChar);
        }
        Console.WriteLine();
        return sb.ToString();
    }
}
