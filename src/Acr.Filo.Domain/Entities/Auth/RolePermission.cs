namespace Acr.Filo.Domain.Entities.Auth;

/// <summary>SQL: dbo.RolePermissions (bileşik PK). Kod deploy'u olmadan yetki değişimini sağlar.</summary>
public class RolePermission
{
    public int RoleId { get; set; }
    public int PermissionId { get; set; }
    public Role Role { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}
