namespace Acr.Filo.Domain.Entities.Auth;

/// <summary>SQL: dbo.Roles. Seed: admin | operasyon | muhasebe</summary>
public class Role
{
    public int Id { get; set; }
    public string Key { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

public static class RoleKeys
{
    public const string Admin     = "admin";
    public const string Operasyon = "operasyon";
    public const string Muhasebe  = "muhasebe";
}
